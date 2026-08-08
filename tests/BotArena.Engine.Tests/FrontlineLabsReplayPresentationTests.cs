namespace BotArena.Engine.Tests;

public sealed class FrontlineLabsReplayPresentationTests
{
    [Fact]
    public void ClassFormsReceiveAuthoredLooksOutsideTheGameplayContract()
    {
        ActorResolvedMatchDefinition definition =
            FrontlineLabsDefinition.CreateClassesExperiment(
                FrontlineLabsClassDefinition.Bulwark,
                FrontlineLabsClassDefinition.Striker);
        string fingerprint =
            ActorContractFingerprint.ComputeMatch(definition);

        GenericActorReplayPresentation presentation =
            FrontlineLabsReplayPresentation.Create(definition);

        Assert.Equal("ember-forge", presentation.ThemeId);
        Assert.Equal("perimeter", presentation.Map?.BoundaryWall);
        Assert.Equal("cover", presentation.Map?.InteriorWall);
        Assert.Empty(presentation.Map?.WallGroups ?? []);
        Assert.Contains(
            presentation.Forms,
            form =>
                form.FormId == "bulwark-prime" &&
                form.LookId == "aegis-tortoise" &&
                form.ProjectileLookId == "rebound-diamond");
        Assert.Contains(
            presentation.Forms,
            form =>
                form.FormId == "bulwark-prime-turret" &&
                form.LookId == "aegis-tortoise-turret");
        Assert.Contains(
            presentation.Forms,
            form =>
                form.FormId == "bulwark-child-turret" &&
                form.LookId == "aegis-tortoise-turret");
        Assert.Contains(
            presentation.Forms,
            form =>
                form.FormId == "striker-prime" &&
                form.LookId == "trident-wasp" &&
                form.ProjectileLookId == "trident-spark");
        Assert.Contains(
            presentation.Forms,
            form =>
                form.FormId == "striker-child" &&
                form.LookId == "trident-wasp");
        Assert.Equal(
            fingerprint,
            ActorContractFingerprint.ComputeMatch(definition));
    }

    [Fact]
    public void AlternateFormsStayInTheirAuthoredChassisFamilies()
    {
        foreach ((
                     FrontlineLabsClassDefinition chassis,
                     FrontlineLabsSkillKit skill,
                     string look) in new[]
                 {
                     (
                         FrontlineLabsClassDefinition.Bulwark,
                         FrontlineLabsSkillKit.BulwarkAegisShell,
                         "aegis-tortoise-shell"),
                     (
                         FrontlineLabsClassDefinition.Striker,
                         FrontlineLabsSkillKit.StrikerVolley,
                         "trident-wasp-volley"),
                 })
        {
            ActorResolvedMatchDefinition definition =
                FrontlineLabsSkillArmTestFixture.Arm(
                    chassis,
                    chassis,
                    skill);
            string fingerprint =
                ActorContractFingerprint.ComputeMatch(definition);

            GenericActorReplayPresentation presentation =
                FrontlineLabsReplayPresentation.Create(definition);

            Assert.Contains(
                presentation.Forms,
                form =>
                    form.FormId == chassis.PrimeStanceFormId &&
                    form.LookId == look);
            Assert.Contains(
                presentation.Forms,
                form =>
                    form.FormId == chassis.ChildStanceFormId &&
                    form.LookId == look);
            Assert.Equal(
                fingerprint,
                ActorContractFingerprint.ComputeMatch(definition));
        }
    }
}
