using ProPosing.Avalonia.Models;

namespace ProPosing.Avalonia.Evaluation;

/// <summary>
/// Reduces visible flicker in the feedback UI by:
/// <list type="bullet">
///   <item>requiring a new status to persist across N frames (hysteresis) before committing,</item>
///   <item>EMA-smoothing each numeric metric,</item>
///   <item>computing a <c>Tightness</c> score from short-window metric variance.</item>
/// </list>
/// Resets when the pose mode changes, so switching poses never inherits stale state.
/// </summary>
public sealed class PoseFeedbackSmoother
{
    private const int  CommitFrames   = 3;     // frames of agreement before status flips
    private const int  TightnessWin   = 12;    // samples in the rolling window
    private const double Alpha        = 0.35;  // EMA weight on latest sample

    private string? _currentMode;
    private PoseFeedback _committed = PoseFeedback.NoDetection;

    private string _pendingStatus = string.Empty;
    private int _pendingCount;

    private readonly EmaMetric _yaw   = new(Alpha);
    private readonly EmaMetric _tilt  = new(Alpha);
    private readonly EmaMetric _twist = new(Alpha);
    private readonly EmaMetric _vtap  = new(Alpha);
    private readonly EmaMetric _sym   = new(Alpha);

    private readonly RollingWindow _twistWin = new(TightnessWin);
    private readonly RollingWindow _tiltWin  = new(TightnessWin);

    public PoseFeedback Apply(string poseMode, PoseFeedback raw)
    {
        if (_currentMode != poseMode)
        {
            Reset();
            _currentMode = poseMode;
        }

        // ── Status hysteresis ────────────────────────────────────────────────
        if (raw.Status == _committed.Status)
        {
            _pendingStatus = string.Empty;
            _pendingCount  = 0;
        }
        else if (raw.Status == _pendingStatus)
        {
            _pendingCount++;
            if (_pendingCount >= CommitFrames)
            {
                _committed = raw;
                _pendingStatus = string.Empty;
                _pendingCount  = 0;
            }
        }
        else
        {
            _pendingStatus = raw.Status;
            _pendingCount  = 1;
        }

        // Hints track the latest frame even before a status flip so the user
        // sees corrective feedback immediately — only the colour changes lag.
        var pre = _committed with { Hints = raw.Hints, Message = raw.Message };

        // ── Metric smoothing ─────────────────────────────────────────────────
        var m = raw.Metrics;
        var smoothed = new PoseMetrics
        {
            BodyYawDeg     = _yaw.Update(m.BodyYawDeg),
            LateralTiltDeg = _tilt.Update(m.LateralTiltDeg),
            TorsoTwistDeg  = _twist.Update(m.TorsoTwistDeg),
            VTaper         = _vtap.Update(m.VTaper),
            Symmetry       = _sym.Update(m.Symmetry),
            Tightness      = ComputeTightness(m),
        };

        return pre.WithMetrics(smoothed);
    }

    private double ComputeTightness(PoseMetrics m)
    {
        _twistWin.Push(m.TorsoTwistDeg);
        _tiltWin.Push(m.LateralTiltDeg);

        double sigmaTwist = _twistWin.StdDev();
        double sigmaTilt  = _tiltWin.StdDev();
        if (double.IsNaN(sigmaTwist) || double.IsNaN(sigmaTilt)) return double.NaN;

        // ~5° jitter → tightness ≈ 0.5; ~10° → 0; 0° → 1.
        double sigma = Math.Max(sigmaTwist, sigmaTilt);
        return Math.Clamp(1.0 - sigma / 10.0, 0.0, 1.0);
    }

    private void Reset()
    {
        _committed = PoseFeedback.NoDetection;
        _pendingStatus = string.Empty;
        _pendingCount  = 0;
        _yaw.Reset(); _tilt.Reset(); _twist.Reset();
        _vtap.Reset(); _sym.Reset();
        _twistWin.Clear(); _tiltWin.Clear();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private sealed class EmaMetric
    {
        private readonly double _alpha;
        private double _ema;
        private bool _init;
        public EmaMetric(double alpha) => _alpha = alpha;

        public double Update(double x)
        {
            if (double.IsNaN(x)) return _init ? _ema : double.NaN;
            if (!_init) { _ema = x; _init = true; }
            else _ema = _alpha * x + (1 - _alpha) * _ema;
            return _ema;
        }

        public void Reset() { _ema = 0; _init = false; }
    }

    private sealed class RollingWindow
    {
        private readonly double[] _buf;
        private int _count;
        private int _head;
        public RollingWindow(int n) => _buf = new double[n];

        public void Push(double x)
        {
            if (double.IsNaN(x)) return;
            _buf[_head] = x;
            _head = (_head + 1) % _buf.Length;
            if (_count < _buf.Length) _count++;
        }

        public double StdDev()
        {
            if (_count < 3) return double.NaN;
            double mean = 0;
            for (int i = 0; i < _count; i++) mean += _buf[i];
            mean /= _count;
            double variance = 0;
            for (int i = 0; i < _count; i++)
            {
                double d = _buf[i] - mean;
                variance += d * d;
            }
            return Math.Sqrt(variance / _count);
        }

        public void Clear() { _count = 0; _head = 0; }
    }
}
