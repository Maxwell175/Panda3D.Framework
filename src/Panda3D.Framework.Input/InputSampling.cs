using System;
using Panda3D.Core;

namespace Panda3D.Framework.Input;

/// <summary>
/// Samples the current device state; the action/binding layer reads through this seam. Buttons are Panda
/// button handles (<c>KeyboardButton.*</c>/<c>MouseButton.*</c>/<c>GamepadButton.*</c>). Polling is
/// physical (raw scan position).
/// </summary>
internal interface IInputSampler
{
    bool IsDown(ButtonId button);
    float Axis(InputDeviceAxis axis);
}

/// <summary>A binding source that evaluates to a scalar.</summary>
internal interface IAxisEvaluable { float Evaluate(IInputSampler sampler); }

/// <summary>A binding source that evaluates to a 2-D vector.</summary>
internal interface IVectorEvaluable { LVector2f Evaluate(IInputSampler sampler); }

/// <summary>Analog signal conditioning (deadzone).</summary>
internal static class Processors
{
    public static float Deadzone1D(float value, float deadzone)
    {
        if (deadzone <= 0f) return value;
        float mag = MathF.Abs(value);
        if (mag < deadzone) return 0f;
        // Rescale so the live range still reaches ±1.
        float scaled = (mag - deadzone) / (1f - deadzone);
        return MathF.CopySign(MathF.Min(scaled, 1f), value);
    }

    public static LVector2f Deadzone2D(float x, float y, float deadzone)
    {
        if (deadzone <= 0f) return new LVector2f(x, y);
        float mag = MathF.Sqrt(x * x + y * y);
        if (mag < deadzone) return new LVector2f(0f, 0f);
        float scaled = MathF.Min((mag - deadzone) / (1f - deadzone), 1f) / mag;
        return new LVector2f(x * scaled, y * scaled);
    }
}
