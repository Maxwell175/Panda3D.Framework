namespace Panda3D.Framework.Input;

/// <summary>Two buttons → float in [-1, 1].</summary>
public sealed class CompositeAxisBinding : IAxisBindingSource, IAxisEvaluable
{
    public CompositeAxisBinding(ButtonId negative, ButtonId positive)
    {
        Negative = negative;
        Positive = positive;
    }

    public ButtonId Negative { get; set; }
    public ButtonId Positive { get; set; }

    float IAxisEvaluable.Evaluate(IInputSampler sampler)
        => (sampler.IsDown(Positive) ? 1f : 0f) - (sampler.IsDown(Negative) ? 1f : 0f);
}
