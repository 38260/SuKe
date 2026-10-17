using SuKe.UI.Shell;

namespace SuKe.Tests;

/// <summary>
/// 滚轮平滑滚动的纯计算部分。这些断言锁死两个容易被写反的约定：Delta 的符号，
/// 以及“同方向累积、反方向以当前实际位置重新定向”的目标计算。
/// </summary>
public class SmoothScrollTests
{
    /// <summary>按 6 位小数比较，避免比例换算的浮点尾差。</summary>
    private static void AssertDips(double expected, double actual) =>
        Assert.Equal(expected, actual, 6);

    [Fact]
    public void One_notch_with_three_lines_scrolls_72_dips_upwards()
    {
        // Delta 为正 = 滚轮远离用户 = 内容向上 = 偏移减小。
        AssertDips(-72, SmoothScrollBehavior.WheelStepDips(120, 3, 600));
        AssertDips(72, SmoothScrollBehavior.WheelStepDips(-120, 3, 600));
    }

    [Fact]
    public void Line_count_from_the_system_is_respected()
    {
        AssertDips(-24, SmoothScrollBehavior.WheelStepDips(120, 1, 600));
        AssertDips(-360, SmoothScrollBehavior.WheelStepDips(120, 15, 600));
    }

    [Fact]
    public void Small_deltas_are_kept_proportional_instead_of_being_rounded_to_notches()
    {
        // 精确触控板常见的 40/80 单位输入必须按比例保留。
        AssertDips(-24, SmoothScrollBehavior.WheelStepDips(40, 3, 600));
        AssertDips(-48, SmoothScrollBehavior.WheelStepDips(80, 3, 600));
        AssertDips(-6, SmoothScrollBehavior.WheelStepDips(10, 3, 600));
    }

    [Fact]
    public void Page_mode_uses_the_viewport_and_keeps_the_sign()
    {
        AssertDips(-720, SmoothScrollBehavior.WheelStepDips(120, -1, 800));
        AssertDips(720, SmoothScrollBehavior.WheelStepDips(-120, -1, 800));

        // 视口极小的时候仍然至少走一行，避免几乎不动。
        AssertDips(-24, SmoothScrollBehavior.WheelStepDips(120, -1, 10));
    }

    [Fact]
    public void Disabling_wheel_scrolling_in_the_system_does_not_turn_it_into_page_mode()
    {
        AssertDips(0, SmoothScrollBehavior.WheelStepDips(120, 0, 800));
        AssertDips(0, SmoothScrollBehavior.WheelStepDips(-120, 0, 800));
    }

    [Fact]
    public void The_same_direction_accumulates_but_a_reversal_re_anchors_at_the_current_offset()
    {
        // 同方向：在上一个目标上继续累积。
        AssertDips(150, SmoothScrollBehavior.NextTarget(72, 72, true, 1, 78, 1000));
        // 反方向：从当前实际位置重新定向，不会先走完旧方向。
        AssertDips(0, SmoothScrollBehavior.NextTarget(72, 150, true, 1, -78, 1000));
    }

    [Fact]
    public void The_target_is_clamped_to_the_scrollable_range()
    {
        AssertDips(0, SmoothScrollBehavior.NextTarget(0, 0, false, 0, -72, 1000));
        AssertDips(200, SmoothScrollBehavior.NextTarget(200, 200, false, 0, 72, 200));
        AssertDips(0, SmoothScrollBehavior.NextTarget(0, 0, false, 0, 72, 0));
    }

    [Fact]
    public void Boundary_detection_uses_the_same_sign_convention()
    {
        // Delta 为负 = 向下：还有内容可向下时才继续。
        Assert.True(SmoothScrollBehavior.CanScrollFurther(offset: 0, scrollableHeight: 500, delta: -120));
        Assert.False(SmoothScrollBehavior.CanScrollFurther(offset: 500, scrollableHeight: 500, delta: -120));
        // Delta 为正 = 向上：需要偏移大于 0。
        Assert.True(SmoothScrollBehavior.CanScrollFurther(offset: 10, scrollableHeight: 500, delta: 120));
        Assert.False(SmoothScrollBehavior.CanScrollFurther(offset: 0, scrollableHeight: 500, delta: 120));
    }
}
