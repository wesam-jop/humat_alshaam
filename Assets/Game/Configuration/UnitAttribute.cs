using UnityEngine;

namespace Adventure3D.Configuration
{
    /// <summary>
    /// Shows the physical unit next to a field label in the Inspector, e.g. "Walk Speed (m/s)", and
    /// optionally clamps the value (Min) or draws a slider (min, max). Drawn by UnitDrawer (Editor).
    /// </summary>
    public sealed class UnitAttribute : PropertyAttribute
    {
        public readonly string Unit;
        public readonly bool HasRange;
        public readonly float RangeMin;
        public readonly float RangeMax;

        /// <summary>Lower bound without a slider. NaN = unbounded.</summary>
        public float Min { get; set; } = float.NaN;

        public UnitAttribute(string unit) { Unit = unit; }

        public UnitAttribute(string unit, float min, float max)
        {
            Unit = unit;
            HasRange = true;
            RangeMin = min;
            RangeMax = max;
        }
    }
}
