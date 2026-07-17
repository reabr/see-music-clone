using SeeMusicClone.Core.Audio;

namespace SeeMusicClone.Core.Tests.Audio;

public sealed class PitchTrackerTests
{
    [Fact]
    public void Update_RequiresConsecutiveDetectionsBeforeShowingNewNote()
    {
        var tracker = new PitchTracker(requiredConfirmations: 2);
        var now = DateTime.UtcNow;

        Assert.Null(tracker.Update(CreatePitch(440, 69), now));
        Assert.Equal(69, tracker.Update(CreatePitch(440.2, 69), now.AddMilliseconds(80))?.MidiNoteNumber);
    }

    [Fact]
    public void Update_HoldsStablePitchAcrossShortDropoutThenClearsIt()
    {
        var tracker = new PitchTracker(requiredConfirmations: 1, holdDuration: TimeSpan.FromMilliseconds(300));
        var now = DateTime.UtcNow;

        tracker.Update(CreatePitch(440, 69), now);

        Assert.Equal(69, tracker.Update(null, now.AddMilliseconds(250))?.MidiNoteNumber);
        Assert.Null(tracker.Update(null, now.AddMilliseconds(301)));
    }

    [Fact]
    public void Update_RequiresConfirmationBeforeSwitchingNotes()
    {
        var tracker = new PitchTracker(requiredConfirmations: 2);
        var now = DateTime.UtcNow;
        tracker.Update(CreatePitch(440, 69), now);
        tracker.Update(CreatePitch(440, 69), now.AddMilliseconds(80));

        Assert.Equal(69, tracker.Update(CreatePitch(493.88, 71), now.AddMilliseconds(160))?.MidiNoteNumber);
        Assert.Equal(71, tracker.Update(CreatePitch(493.88, 71), now.AddMilliseconds(240))?.MidiNoteNumber);
    }

    [Fact]
    public void Update_UnconfirmedNotesDoNotKeepOldPitchAlive()
    {
        var tracker = new PitchTracker(requiredConfirmations: 2, holdDuration: TimeSpan.FromMilliseconds(300));
        var now = DateTime.UtcNow;
        tracker.Update(CreatePitch(440, 69), now);
        tracker.Update(CreatePitch(440, 69), now.AddMilliseconds(80));

        tracker.Update(CreatePitch(493.88, 71), now.AddMilliseconds(250));

        Assert.Null(tracker.Update(null, now.AddMilliseconds(381)));
    }

    private static PitchDetectionResult CreatePitch(double frequencyHz, int midiNoteNumber)
    {
        return new PitchDetectionResult
        {
            FrequencyHz = frequencyHz,
            MidiNoteNumber = midiNoteNumber,
            Confidence = 0.95
        };
    }
}
