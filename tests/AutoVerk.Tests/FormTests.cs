using AutoVerk.Engine;
using AutoVerk.Ui;
using Xunit;

namespace AutoVerk.Tests
{
    [Collection("registry")]
    public class FormTests
    {
        private static SkillInfo Info() => new SkillInfo
        {
            Id = "test.form",
            Description = "test",
            Params = { Param.Item("crop", "the crop"), Param.Number("radius", "metres", 50), Param.Item("only", "filter", "") },
            Create = args => new Wait(args.Number("radius")),
        };

        private static void WithSkill(System.Action test)
        {
            Registry.Register(Info());
            try { test(); }
            finally { Registry.Unregister("test.form"); }
        }

        [Fact]
        public void ShowsDefaultsAndKeepsWhatWasTyped()
        {
            WithSkill(() =>
            {
                var form = new SkillForm();
                form.Restore("test.form");
                var radius = form.Skill.Params[1];
                Assert.Equal("50", form.Value(radius));
                form.Set(radius, "12");
                Assert.Equal("12", form.Value(radius));
            });
        }

        [Fact]
        public void AnEmptyFieldIsNotGiven()
        {
            WithSkill(() =>
            {
                var form = new SkillForm();
                form.Restore("test.form");
                Assert.Null(form.Create(out var error));
                Assert.StartsWith("crop is required", error);

                form.Set(form.Skill.Params[0], "Carrot");
                form.Set(form.Skill.Params[1], "");
                var skill = form.Create(out error);
                Assert.Null(error);
                Assert.Equal("wait 50 s", skill.ToString());
            });
        }

        [Fact]
        public void RestoresTheLastStartFromItsLine()
        {
            WithSkill(() =>
            {
                var form = new SkillForm();
                form.Restore("test.form crop=\"Fine wood\" radius=7");
                Assert.Equal("test.form", form.Skill.Id);
                Assert.Equal("test.form crop=\"Fine wood\" radius=7 only=", form.Line);

                var again = new SkillForm();
                again.Restore(form.Line);
                Assert.Equal("Fine wood", again.Value(again.Skill.Params[0]));
            });
        }

        [Fact]
        public void LeavesOutDeveloperSkills()
        {
            Registry.Register(new SkillInfo { Id = "test.dev", Create = _ => new Wait(1), ForDevelopers = true });
            try
            {
                Assert.DoesNotContain(Registry.Skills, s => s.Id == "test.dev");
                Assert.Contains(Registry.AllSkills, s => s.Id == "test.dev");
                Assert.NotNull(Registry.FromLine("test.dev", out _));

                var form = new SkillForm();
                form.Refresh();
                form.Restore("test.dev");
                Assert.NotEqual("test.dev", form.Skill?.Id);
                Assert.DoesNotContain(form.Skills, s => s.Id == "test.dev");
            }
            finally
            {
                Registry.Unregister("test.dev");
            }
        }

        [Fact]
        public void ACreateThatThrowsBecomesAnError()
        {
            Registry.Register(new SkillInfo { Id = "test.throws", Create = _ => throw new System.InvalidOperationException("not in a world") });
            try
            {
                var form = new SkillForm();
                form.Restore("test.throws");
                Assert.Null(form.Create(out var error));
                Assert.Equal("test.throws couldn't start: not in a world", error);
            }
            finally
            {
                Registry.Unregister("test.throws");
            }
        }
    }
}
