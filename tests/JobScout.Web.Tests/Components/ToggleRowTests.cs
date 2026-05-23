using Bunit;
using JobScout.Web.Components;

namespace JobScout.Web.Tests.Components;

public class ToggleRowTests : TestContext
{
    [Fact]
    public void Renders_LabelAndDescription()
    {
        var cut = RenderComponent<ToggleRow>(p => p
            .Add(r => r.Label, "Daily digest")
            .Add(r => r.Description, "Top fits from the last 24h")
            .Add(r => r.Checked, false));

        cut.Markup.Should().Contain("Daily digest");
        cut.Markup.Should().Contain("Top fits from the last 24h");
    }

    [Fact]
    public void OmitsDescription_WhenNullOrEmpty()
    {
        var cut = RenderComponent<ToggleRow>(p => p
            .Add(r => r.Label, "Just a label")
            .Add(r => r.Checked, true));

        cut.Markup.Should().NotContain("toggle-row-desc");
    }

    [Fact]
    public void CheckedChanged_FiresWithNewValue()
    {
        bool? captured = null;
        var cut = RenderComponent<ToggleRow>(p => p
            .Add(r => r.Label, "Toggle")
            .Add(r => r.Checked, false)
            .Add(r => r.CheckedChanged, v => captured = v));

        var input = cut.Find("input[type=checkbox]");
        input.Change(true);

        captured.Should().BeTrue();
    }

    [Fact]
    public void Renders_CheckedState_WhenTrue()
    {
        var cut = RenderComponent<ToggleRow>(p => p
            .Add(r => r.Label, "On")
            .Add(r => r.Checked, true));

        var input = cut.Find("input[type=checkbox]");
        input.HasAttribute("checked").Should().BeTrue();
    }
}
