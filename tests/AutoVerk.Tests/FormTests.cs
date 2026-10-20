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

        [Fact]
        public void PreviewsWithTheFieldsFilledIn()
        {
            Registry.Register(new SkillInfo
            {
                Id = "test.preview",
                Params = { Param.Item("crop", "the crop"), Param.Number("radius", "metres", 50) },
                Create = _ => new Wait(1),
                Preview = args => $"{args.Item("crop")} within {args.Number("radius")} m",
            });
            try
            {
                var form = new SkillForm();
                form.Restore("test.preview");
                Assert.True(form.CanPreview);
                Assert.StartsWith("crop is required", form.Preview());

                form.Set(form.Skill.Params[0], "Carrot");
                Assert.Equal("Carrot within 50 m", form.Preview());
                Assert.Equal("Carrot within 7 m", Registry.PreviewLine("test.preview crop=Carrot radius=7"));
            }
            finally
            {
                Registry.Unregister("test.preview");
            }
        }

        [Fact]
        public void ValuesReadTheFieldsAsTheyStand()
        {
            WithSkill(() =>
            {
                var form = new SkillForm();
                form.Restore("test.form crop=Carrot radius=abc");
                var v = form.Values;
                Assert.Equal("Carrot", v.Item("crop"));
                Assert.Null(v.Number("radius"));
                Assert.Equal("", v.Text("nothing"));

                form.Set(form.Skill.Params[1], "7.5");
                Assert.Equal(7.5f, form.Values.Number("radius"));
            });
        }

        [Fact]
        public void TitleAndDoingFallBackToTheId()
        {
            var info = Info();
            Assert.Equal("test.form", info.Title);
            Assert.Equal("test.form", info.Doing);
            info.Title = "Test a form";
            Assert.Equal("Test a form", info.Doing);
            Assert.Equal("Start", info.StartText);
        }

        [Fact]
        public void ASkillStartedByNameKnowsItsInfo()
        {
            WithSkill(() =>
            {
                Assert.Equal("test.form", Registry.FromLine("test.form crop=Carrot", out _).Info.Id);
                var form = new SkillForm();
                form.Restore("test.form crop=Carrot");
                Assert.Equal("test.form", form.Create(out _).Info.Id);
            });
        }

        [Fact]
        public void APressedButtonActsOnTheValueAndCanClearIt()
        {
            string deleted = null;
            Registry.Register(new SkillInfo
            {
                Id = "test.button",
                Params =
                {
                    Param.Text("field", "a saved one", "").WithButton("Delete", v =>
                    {
                        deleted = v.Text("field");
                        return $"deleted {deleted}";
                    }, v => v.Text("field") != "", confirm: true, clears: true),
                },
                Create = _ => new Wait(1),
            });
            try
            {
                var form = new SkillForm();
                form.Restore("test.button field=carrots-1");
                int revision = form.Revision;
                var field = form.Skill.Params[0];
                Assert.Equal("deleted carrots-1", form.Press(field));
                Assert.Equal("carrots-1", deleted);
                Assert.Equal("", form.Value(field));
                Assert.True(form.Revision > revision);
            }
            finally
            {
                Registry.Unregister("test.button");
            }
        }

        [Fact]
        public void SaysWhenASkillHasNoPreview()
        {
            WithSkill(() =>
            {
                var form = new SkillForm();
                form.Restore("test.form");
                Assert.False(form.CanPreview);
                Assert.Equal("test.form has no preview", form.Preview());
                Assert.Equal("test.form has no preview", Registry.PreviewLine("test.form crop=Carrot"));
            });
        }
    }
}
