namespace Labeling.Domain.Entities;

/// <summary>
/// Defines the orientation of the physical media relative to the printer head.
/// </summary>
public enum LabelMediaOrientation
{
    /// <summary>
    /// Normal orientation. Top of the label comes out first.
    /// Used when label width &lt; printer width.
    /// </summary>
    Portrait = 0,

    /// <summary>
    /// Rotated 90 degrees.
    /// Used when label is wider than printer head, or strictly fed horizontally.
    /// For 55x90mm label fed horizontally, this is likely required.
    /// </summary>
    Landscape = 1
}

