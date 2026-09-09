namespace EatsVoiceCompanion.Core.Speech;

public enum VoiceInstructionType
{
    FlyHeading,
    TurnLeftHeading,
    TurnRightHeading,
    ClimbAndMaintain,
    DescendAndMaintain,
    DescendVia,
    DescendViaExceptMaintain,
    DescendAtPilotsDiscretion,
    Expedite,
    ExpediteThroughAltitude,
    ReportLeavingAltitude,
    ReportReachingAltitude,
    SayAltitude,
    MaintainSpeed,
    MaintainSpeedOrGreater,
    MaintainSpeedOrLess,
    MaintainMach,
    MaintainMachOrGreater,
    MaintainMachOrLess,
    ResumeNormalSpeed,
    SayIndicatedSpeed,
    SayMach,
    SayNormalSpeed,
    ComplyWithPublishedSpeeds,
    ProceedDirect,
    CrossAtAltitude,
    CrossAtAltitudeAndSpeed,
    Altimeter,
    Roger
}
