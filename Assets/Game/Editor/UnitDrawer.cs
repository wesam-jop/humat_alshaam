using Adventure3D.Configuration;
using UnityEditor;
using UnityEngine;

namespace Adventure3D.EditorTools
{
    /// <summary>Inspector label "Walk Speed (m/s)" + optional slider / lower bound.</summary>
    [CustomPropertyDrawer(typeof(UnitAttribute))]
    public sealed class UnitDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var unit = (UnitAttribute)attribute;
            var content = new GUIContent(label.text + " (" + unit.Unit + ")", label.tooltip);
            EditorGUI.BeginProperty(position, content, property);
            if (property.propertyType == SerializedPropertyType.Float)
            {
                if (unit.HasRange) property.floatValue = EditorGUI.Slider(position, content, property.floatValue, unit.RangeMin, unit.RangeMax);
                else
                {
                    float v = EditorGUI.FloatField(position, content, property.floatValue);
                    if (!float.IsNaN(unit.Min)) v = Mathf.Max(unit.Min, v);
                    property.floatValue = v;
                }
            }
            else if (property.propertyType == SerializedPropertyType.Integer)
            {
                if (unit.HasRange) property.intValue = EditorGUI.IntSlider(position, content, property.intValue, (int)unit.RangeMin, (int)unit.RangeMax);
                else
                {
                    int v = EditorGUI.IntField(position, content, property.intValue);
                    if (!float.IsNaN(unit.Min)) v = Mathf.Max((int)unit.Min, v);
                    property.intValue = v;
                }
            }
            else
            {
                EditorGUI.PropertyField(position, property, content);
            }
            EditorGUI.EndProperty();
        }
    }
}
