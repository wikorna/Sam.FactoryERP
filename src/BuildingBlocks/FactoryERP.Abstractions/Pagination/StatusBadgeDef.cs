namespace FactoryERP.Abstractions.Pagination;

/// <summary>
/// Core states for UI status badges (chips) mapping to Angular Material or Fiori colors.
/// Fiori aligns to: None (Grey), Information (Blue), Success (Green), Warning (Orange), Error (Red).
/// </summary>
public enum UiBadgeState
{
    None = 0,
    Information = 1,
    Success = 2,
    Warning = 3,
    Error = 4
}

/// <summary>
/// A standardized status option to be used in frontend filter bars or detail headers.
/// </summary>
public sealed record StatusBadgeDef(string Id, string Text, UiBadgeState State);
