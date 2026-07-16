using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using SeeMusicClone.Core.Models;

namespace SeeMusicClone.Core.Midi;

/// <summary>
/// Loads a .mid/.midi file and flattens it into a list of absolute-time PianoNotes,
/// filtered to the 88-key piano range.
/// </summary>
public static class MidiLoader
{
    public static MidiSong Load(string filePath)
    {
        var midiFile = MidiFile.Read(filePath);
        var tempoMap = midiFile.GetTempoMap();

        var notes = new List<PianoNote>();
        double maxEnd = 0;

        foreach (var note in midiFile.GetNotes())
        {
            if (note.Channel == 9)
                continue; // GM percussion channel — note numbers are drum IDs, not pitches

            if (note.NoteNumber < PianoLayoutHelper.LowestNote || note.NoteNumber > PianoLayoutHelper.HighestNote)
                continue; // outside 88-key range, ignore for the visualization

            var startMetric = note.TimeAs<MetricTimeSpan>(tempoMap);
            var endMetric = TimeConverter.ConvertTo<MetricTimeSpan>(note.Time + note.Length, tempoMap);

            var start = startMetric.TotalMicroseconds / 1_000_000.0;
            var end = endMetric.TotalMicroseconds / 1_000_000.0;

            notes.Add(new PianoNote
            {
                NoteNumber = note.NoteNumber,
                StartTimeSeconds = start,
                DurationSeconds = Math.Max(0.01, end - start),
                Velocity = note.Velocity,
                Channel = note.Channel
            });

            if (end > maxEnd) maxEnd = end;
        }

        notes.Sort((a, b) => a.StartTimeSeconds.CompareTo(b.StartTimeSeconds));

        return new MidiSong
        {
            FilePath = filePath,
            Notes = notes,
            DurationSeconds = maxEnd + 1.0 // small tail so last note fully falls through
        };
    }

    public static IEnumerable<string> FindMidiFiles(string folder)
    {
        return Directory.EnumerateFiles(folder, "*.*", SearchOption.TopDirectoryOnly)
            .Where(f => f.EndsWith(".mid", StringComparison.OrdinalIgnoreCase)
                     || f.EndsWith(".midi", StringComparison.OrdinalIgnoreCase));
    }
}
