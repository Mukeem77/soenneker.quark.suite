using Soenneker.Quark.Enums;

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Soenneker.Utils.PooledStringBuilders;


namespace Soenneker.Quark;

/// <summary>
/// High-performance position builder with fluent API for chaining position rules.
/// </summary>
public sealed class PositionBuilder : ICssBuilder
{
    private readonly List<PositionRule> _rules = new(4);

    // ----- Class name constants -----
    private const string _classStatic = "position-static";
    private const string _classRelative = "position-relative";
    private const string _classAbsolute = "position-absolute";
    private const string _classFixed = "position-fixed";
    private const string _classSticky = "position-sticky";

    // ----- CSS prefix -----
    private const string _positionPrefix = "position: ";

    internal PositionBuilder(string position, BreakpointType? breakpoint = null)
    {
        _rules.Add(new PositionRule(position, breakpoint));
    }

    internal PositionBuilder(List<PositionRule> rules)
    {
        if (rules is { Count: > 0 })
            _rules.AddRange(rules);
    }

    /// <summary>Chain with static positioning for the next rule.</summary>
    public PositionBuilder Static => ChainWithPosition(PositionKeyword.StaticValue);
    /// <summary>Chain with relative positioning for the next rule.</summary>
    public PositionBuilder Relative => ChainWithPosition(PositionKeyword.RelativeValue);
    /// <summary>Chain with absolute positioning for the next rule.</summary>
    public PositionBuilder Absolute => ChainWithPosition(PositionKeyword.AbsoluteValue);
    /// <summary>Chain with fixed positioning for the next rule.</summary>
    public PositionBuilder Fixed => ChainWithPosition(PositionKeyword.FixedValue);
    /// <summary>Chain with sticky positioning for the next rule.</summary>
    public PositionBuilder Sticky => ChainWithPosition(PositionKeyword.StickyValue);

    public PositionBuilder Inherit => ChainWithPosition(GlobalKeyword.InheritValue);
    public PositionBuilder Initial => ChainWithPosition(GlobalKeyword.InitialValue);
    public PositionBuilder Revert => ChainWithPosition(GlobalKeyword.RevertValue);
    public PositionBuilder RevertLayer => ChainWithPosition(GlobalKeyword.RevertLayerValue);
    public PositionBuilder Unset => ChainWithPosition(GlobalKeyword.UnsetValue);

    // ----- BreakpointType chaining -----
    public PositionBuilder OnPhone => ChainWithBreakpoint(BreakpointType.Phone);
    public PositionBuilder OnTablet => ChainWithBreakpoint(BreakpointType.Tablet);
    public PositionBuilder OnLaptop => ChainWithBreakpoint(BreakpointType.Laptop);
    public PositionBuilder OnDesktop => ChainWithBreakpoint(BreakpointType.Desktop);
    public PositionBuilder OnWidescreen => ChainWithBreakpoint(BreakpointType.Widescreen);
    public PositionBuilder OnUltrawide => ChainWithBreakpoint(BreakpointType.Ultrawide);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private PositionBuilder ChainWithPosition(string position)
    {
        _rules.Add(new PositionRule(position, null));
        return this;
    }

    /// <summary>Apply a BreakpointType to the most recent rule (or bootstrap with "static" if empty).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private PositionBuilder ChainWithBreakpoint(BreakpointType breakpoint)
    {
        if (_rules.Count == 0)
        {
            _rules.Add(new PositionRule(PositionKeyword.StaticValue, breakpoint));
            return this;
        }

        var lastIdx = _rules.Count - 1;
        var last = _rules[lastIdx];
        _rules[lastIdx] = new PositionRule(last.Position, breakpoint);
        return this;
    }

    /// <summary>Gets the CSS class string for the current configuration.</summary>
    public string ToClass()
    {
        if (_rules.Count == 0)
            return string.Empty;

        using var sb = new PooledStringBuilder();
        var first = true;

        for (var i = 0; i < _rules.Count; i++)
        {
            var rule = _rules[i];

            var baseClass = GetPositionClass(rule.Position);
            if (baseClass.Length == 0)
                continue;

            var bp = BreakpointUtil.GetBreakpointToken(rule.breakpoint);
            if (bp.Length != 0)
                baseClass = InsertBreakpointType(baseClass, bp);

            if (!first)
                sb.Append(' ');
            else
                first = false;

            sb.Append(baseClass);
        }

        return sb.ToString();
    }

    /// <summary>Gets the CSS style string for the current configuration.</summary>
    public string ToStyle()
    {
        if (_rules.Count == 0)
            return string.Empty;

        using var sb = new PooledStringBuilder();
        var first = true;

        for (var i = 0; i < _rules.Count; i++)
        {
            var rule = _rules[i];
            if (rule.Position.Length == 0)
                continue;

            if (!first)
                sb.Append("; ");
            else
                first = false;

            sb.Append(_positionPrefix);
            sb.Append(rule.Position);
        }

        return sb.ToString();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string GetPositionClass(string position)
    {
        return position switch
        {
            // Intellenum<string> *Value constants are compile-time consts, safe in switch
            PositionKeyword.StaticValue => _classStatic,
            PositionKeyword.RelativeValue => _classRelative,
            PositionKeyword.AbsoluteValue => _classAbsolute,
            PositionKeyword.FixedValue => _classFixed,
            PositionKeyword.StickyValue => _classSticky,
            _ => string.Empty
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string GetBp(BreakpointType? breakpoint) => breakpoint?.Value ?? string.Empty;

    /// <summary>
    /// Insert BreakpointType token as: "position-fixed" + "md" ? "position-md-fixed".
    /// Falls back to "bp-{class}" if no dash exists.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string InsertBreakpointType(string className, string bp)
    {
        var dashIndex = className.IndexOf('-');
        if (dashIndex > 0)
        {
            // length = prefix + "-" + bp + remainder
            var len = dashIndex + 1 + bp.Length + (className.Length - dashIndex);
            return string.Create(len, (className, dashIndex, bp), static (dst, s) =>
            {
                // prefix
                s.className.AsSpan(0, s.dashIndex).CopyTo(dst);
                var idx = s.dashIndex;

                // "-" + bp
                dst[idx++] = '-';
                s.bp.AsSpan().CopyTo(dst[idx..]);
                idx += s.bp.Length;

                // remainder (starts with '-')
                s.className.AsSpan(s.dashIndex).CopyTo(dst[idx..]);
            });
        }

        // Fallback: "bp-{className}"
        return string.Create(bp.Length + 1 + className.Length, (className, bp), static (dst, s) =>
        {
            s.bp.AsSpan().CopyTo(dst);
            var idx = s.bp.Length;
            dst[idx++] = '-';
            s.className.AsSpan().CopyTo(dst[idx..]);
        });
    }
}
