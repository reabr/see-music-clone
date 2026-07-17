namespace SeeMusicClone.Core.Audio;

public sealed class PitchTracker
{
    private readonly int _requiredConfirmations;
    private readonly TimeSpan _holdDuration;
    private readonly double _smoothingFactor;

    private PitchDetectionResult? _currentPitch;
    private int? _candidateNoteNumber;
    private int _candidateCount;
    private DateTime _lastValidPitchUtc = DateTime.MinValue;

    public PitchTracker(
        int requiredConfirmations = 2,
        TimeSpan? holdDuration = null,
        double smoothingFactor = 0.35)
    {
        if (requiredConfirmations < 1)
            throw new ArgumentOutOfRangeException(nameof(requiredConfirmations));
        if (smoothingFactor is <= 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(smoothingFactor));

        _requiredConfirmations = requiredConfirmations;
        _holdDuration = holdDuration ?? TimeSpan.FromMilliseconds(300);
        _smoothingFactor = smoothingFactor;

        if (_holdDuration < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(holdDuration));
    }

    public PitchDetectionResult? Update(PitchDetectionResult? pitch, DateTime timestampUtc)
    {
        if (pitch == null)
        {
            if (_currentPitch != null && timestampUtc - _lastValidPitchUtc <= _holdDuration)
                return _currentPitch;

            Reset();
            return null;
        }

        if (_currentPitch?.MidiNoteNumber == pitch.MidiNoteNumber)
        {
            _lastValidPitchUtc = timestampUtc;
            _currentPitch = Blend(_currentPitch, pitch);
            SetCandidate(pitch.MidiNoteNumber, _requiredConfirmations);
            return _currentPitch;
        }

        if (_candidateNoteNumber == pitch.MidiNoteNumber)
            _candidateCount++;
        else
            SetCandidate(pitch.MidiNoteNumber, 1);

        if (_candidateCount >= _requiredConfirmations)
        {
            _currentPitch = pitch;
            _lastValidPitchUtc = timestampUtc;
        }

        return _currentPitch;
    }

    public void Reset()
    {
        _currentPitch = null;
        _candidateNoteNumber = null;
        _candidateCount = 0;
        _lastValidPitchUtc = DateTime.MinValue;
    }

    private PitchDetectionResult Blend(PitchDetectionResult current, PitchDetectionResult next)
    {
        var inverse = 1.0 - _smoothingFactor;
        return new PitchDetectionResult
        {
            FrequencyHz = current.FrequencyHz * inverse + next.FrequencyHz * _smoothingFactor,
            MidiNoteNumber = next.MidiNoteNumber,
            Confidence = current.Confidence * inverse + next.Confidence * _smoothingFactor
        };
    }

    private void SetCandidate(int noteNumber, int count)
    {
        _candidateNoteNumber = noteNumber;
        _candidateCount = count;
    }
}
