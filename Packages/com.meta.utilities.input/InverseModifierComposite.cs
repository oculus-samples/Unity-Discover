// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using System.ComponentModel;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.Utilities;

// based on com.unity.inputsystem/InputSystem/Actions/Composites/OneModifierComposite.cs

[DisplayStringFormat("(NOT {modifier})+{binding}")]
[DisplayName("Binding With One Inverse Modifier")]
public class InverseModifierComposite : InputBindingComposite
{
    [RuntimeInitializeOnLoadMethod]
#if UNITY_EDITOR
    [UnityEditor.InitializeOnLoadMethod]
#endif
    private static void Initialize()
    {
        InputSystem.RegisterBindingComposite<InverseModifierComposite>("InverseModifierComposite");
    }

    private bool ReadModifierValue(InputBindingCompositeContext context) => !context.ReadValueAsButton(m_modifier);

    /// <summary>
    /// Binding for the button that acts as a modifier, e.g. <c>&lt;Keyboard/ctrl</c>.
    /// </summary>
    /// <value>Part index to use with <see cref="InputBindingCompositeContext.ReadValue{T}(int)"/>.</value>
    /// <remarks>
    /// This property is automatically assigned by the input system.
    /// </remarks>
    // ReSharper disable once MemberCanBePrivate.Global
    // ReSharper disable once FieldCanBeMadeReadOnly.Global
    // ReSharper disable once UnassignedField.Global
    [InputControl(layout = "Button")] public int m_modifier;

    /// <summary>
    /// Binding for the control that is gated by the modifier. The composite will assume the value
    /// of this control while the modifier is considered pressed (that is, has a magnitude equal to or
    /// greater than the button press point).
    /// </summary>
    /// <value>Part index to use with <see cref="InputBindingCompositeContext.ReadValue{T}(int)"/>.</value>
    /// <remarks>
    /// This property is automatically assigned by the input system.
    /// </remarks>
    // ReSharper disable once MemberCanBePrivate.Global
    // ReSharper disable once FieldCanBeMadeReadOnly.Global
    // ReSharper disable once UnassignedField.Global
    [InputControl] public int m_binding;

    /// <summary>
    /// Type of values read from controls bound to <see cref="m_binding"/>.
    /// </summary>
    public override Type valueType => m_valueType;

    /// <summary>
    /// Size of the largest value that may be read from the controls bound to <see cref="m_binding"/>.
    /// </summary>
    public override int valueSizeInBytes => m_valueSizeInBytes;

    private int m_valueSizeInBytes;
    private Type m_valueType;

    public override float EvaluateMagnitude(ref InputBindingCompositeContext context) =>
        ReadModifierValue(context) ? context.EvaluateMagnitude(m_binding) : default;

    /// <inheritdoc/>
    public override unsafe void ReadValue(ref InputBindingCompositeContext context, void* buffer, int bufferSize)
    {
        if (ReadModifierValue(context))
            context.ReadValue(m_binding, buffer, bufferSize);
        else
            UnsafeUtility.MemClear(buffer, m_valueSizeInBytes);
    }

    /// <inheritdoc/>
    protected override void FinishSetup(ref InputBindingCompositeContext context)
    {
        DetermineValueTypeAndSize(ref context, m_binding, out m_valueType, out m_valueSizeInBytes);
    }

    public override object ReadValueAsObject(ref InputBindingCompositeContext context) =>
        ReadModifierValue(context) ? context.ReadValueAsObject(m_binding) : default;

    internal static void DetermineValueTypeAndSize(ref InputBindingCompositeContext context, int part, out Type valueType, out int valueSizeInBytes)
    {
        valueSizeInBytes = 0;

        Type type = null;
        foreach (var control in context.controls)
        {
            if (control.part != part)
                continue;

            var controlType = control.control.valueType;
            if (type == null || controlType.IsAssignableFrom(type))
                type = controlType;
            else if (!type.IsAssignableFrom(controlType))
                type = typeof(object);

            valueSizeInBytes = Math.Max(control.control.valueSizeInBytes, valueSizeInBytes);
        }

        valueType = type;
    }
}
