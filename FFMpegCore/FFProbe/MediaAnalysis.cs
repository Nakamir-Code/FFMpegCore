using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using FFMpegCore.Builders.MetaData;

namespace FFMpegCore
{
    public class MediaAnalysis : IMediaAnalysis
    {
        [JsonConstructor]
        public MediaAnalysis()
        {
        }

        public MediaAnalysis(FFProbeAnalysis analysis)
        {
            Format = ParseFormat(analysis.Format);
            Chapters = analysis.Chapters.Select(c => ParseChapter(c)).ToList();
            VideoStreams = analysis.Streams.Where(stream => stream.CodecType == "video").Select(ParseVideoStream).ToList();
            AudioStreams = analysis.Streams.Where(stream => stream.CodecType == "audio").Select(ParseAudioStream).ToList();
            SubtitleStreams = analysis.Streams.Where(stream => stream.CodecType == "subtitle").Select(ParseSubtitleStream).ToList();
            ErrorData = analysis.ErrorData;
        }

        private MediaFormat ParseFormat(Format analysisFormat)
        {
            return new MediaFormat
            {
                Duration = MediaAnalysisUtils.ParseDuration(analysisFormat.Duration),
                StartTime = MediaAnalysisUtils.ParseDuration(analysisFormat.StartTime),
                FormatName = analysisFormat.FormatName,
                FormatLongName = analysisFormat.FormatLongName,
                StreamCount = analysisFormat.NbStreams,
                ProbeScore = analysisFormat.ProbeScore,
                BitRate = MediaAnalysisUtils.ParseLongInvariant(analysisFormat.BitRate ?? "0"),
                Tags = analysisFormat.Tags.ToCaseInsensitive(),
            };
        }

        private string GetValue(string tagName, Dictionary<string, string>? tags, string defaultValue) =>
            tags == null ? defaultValue : tags.TryGetValue(tagName, out var value) ? value : defaultValue;

        private ChapterData ParseChapter(Chapter analysisChapter)
        {
            var title = GetValue("title", analysisChapter.Tags, "TitleValueNotSet");
            var start = MediaAnalysisUtils.ParseDuration(analysisChapter.StartTime);
            var end = MediaAnalysisUtils.ParseDuration(analysisChapter.EndTime);

            return new ChapterData(title, start, end);
        }

        public TimeSpan Duration => new[]
        {
            Format.Duration,
            PrimaryVideoStream?.Duration ?? TimeSpan.Zero,
            PrimaryAudioStream?.Duration ?? TimeSpan.Zero
        }.Max();

        public MediaFormat Format { get; set; }

        public List<ChapterData> Chapters { get; set; }

        public AudioStream? PrimaryAudioStream => AudioStreams.OrderBy(stream => stream.Index).FirstOrDefault();
        public VideoStream? PrimaryVideoStream => VideoStreams.OrderBy(stream => stream.Index).FirstOrDefault();
        public SubtitleStream? PrimarySubtitleStream => SubtitleStreams.OrderBy(stream => stream.Index).FirstOrDefault();

        public List<VideoStream> VideoStreams { get; set; }
        public List<AudioStream> AudioStreams { get; set; }
        public List<SubtitleStream> SubtitleStreams { get; set; }
        public IReadOnlyList<string> ErrorData { get; set; }

        private int? GetBitDepth(FFProbeStream stream)
        {
            var bitDepth = int.TryParse(stream.BitsPerRawSample, out var bprs) ? bprs :
                stream.BitsPerSample;
            return bitDepth == 0 ? null : bitDepth;
        }

        private VideoStream ParseVideoStream(FFProbeStream stream)
        {
            return new VideoStream
            {
                Index = stream.Index,
                AvgFrameRate = MediaAnalysisUtils.DivideRatio(MediaAnalysisUtils.ParseRatioDouble(stream.AvgFrameRate, '/')),
                BitRate = !string.IsNullOrEmpty(stream.BitRate) ? MediaAnalysisUtils.ParseLongInvariant(stream.BitRate) : default,
                BitsPerRawSample = !string.IsNullOrEmpty(stream.BitsPerRawSample) ? MediaAnalysisUtils.ParseIntInvariant(stream.BitsPerRawSample) : default,
                CodecName = stream.CodecName,
                CodecLongName = stream.CodecLongName,
                CodecTag = stream.CodecTag,
                CodecTagString = stream.CodecTagString,
                DisplayAspectRatio = MediaAnalysisUtils.ParseRatioInt(stream.DisplayAspectRatio, ':'),
                SampleAspectRatio = MediaAnalysisUtils.ParseRatioInt(stream.SampleAspectRatio, ':'),
                Duration = MediaAnalysisUtils.ParseDuration(stream.Duration),
                StartTime = MediaAnalysisUtils.ParseDuration(stream.StartTime),
                FrameRate = MediaAnalysisUtils.DivideRatio(MediaAnalysisUtils.ParseRatioDouble(stream.FrameRate, '/')),
                Height = stream.Height ?? 0,
                Width = stream.Width ?? 0,
                Profile = stream.Profile,
                PixelFormat = stream.PixelFormat,
                Level = stream.Level,
                ColorRange = stream.ColorRange,
                ColorSpace = stream.ColorSpace,
                ColorTransfer = stream.ColorTransfer,
                ColorPrimaries = stream.ColorPrimaries,
                Rotation = MediaAnalysisUtils.ParseRotation(stream),
                Language = stream.GetLanguage(),
                Disposition = MediaAnalysisUtils.FormatDisposition(stream.Disposition),
                Tags = stream.Tags.ToCaseInsensitive(),
                BitDepth = GetBitDepth(stream),
            };
        }

        private AudioStream ParseAudioStream(FFProbeStream stream)
        {
            return new AudioStream
            {
                Index = stream.Index,
                BitRate = !string.IsNullOrEmpty(stream.BitRate) ? MediaAnalysisUtils.ParseLongInvariant(stream.BitRate) : default,
                CodecName = stream.CodecName,
                CodecLongName = stream.CodecLongName,
                CodecTag = stream.CodecTag,
                CodecTagString = stream.CodecTagString,
                Channels = stream.Channels ?? default,
                ChannelLayout = stream.ChannelLayout,
                Duration = MediaAnalysisUtils.ParseDuration(stream.Duration),
                StartTime = MediaAnalysisUtils.ParseDuration(stream.StartTime),
                SampleRateHz = !string.IsNullOrEmpty(stream.SampleRate) ? MediaAnalysisUtils.ParseIntInvariant(stream.SampleRate) : default,
                Profile = stream.Profile,
                Language = stream.GetLanguage(),
                Disposition = MediaAnalysisUtils.FormatDisposition(stream.Disposition),
                Tags = stream.Tags.ToCaseInsensitive(),
                BitDepth = GetBitDepth(stream),
            };
        }

        private SubtitleStream ParseSubtitleStream(FFProbeStream stream)
        {
            return new SubtitleStream
            {
                Index = stream.Index,
                BitRate = !string.IsNullOrEmpty(stream.BitRate) ? MediaAnalysisUtils.ParseLongInvariant(stream.BitRate) : default,
                CodecName = stream.CodecName,
                CodecLongName = stream.CodecLongName,
                Duration = MediaAnalysisUtils.ParseDuration(stream.Duration),
                StartTime = MediaAnalysisUtils.ParseDuration(stream.StartTime),
                Language = stream.GetLanguage(),
                Disposition = MediaAnalysisUtils.FormatDisposition(stream.Disposition),
                Tags = stream.Tags.ToCaseInsensitive(),
            };
        }
    }

    public static class MediaAnalysisUtils
    {
        private static readonly Regex DurationRegex = new(@"^(\d+):(\d{1,2}):(\d{1,2})\.(\d{1,3})", RegexOptions.Compiled);

        internal static Dictionary<string, string> ToCaseInsensitive(this Dictionary<string, string>? dictionary)
        {
            return dictionary?.ToDictionary(tag => tag.Key, tag => tag.Value, StringComparer.OrdinalIgnoreCase) ?? new Dictionary<string, string>();
        }
        public static double DivideRatio((double, double) ratio)
        {
            if (double.IsNaN(ratio.Item1) || double.IsNaN(ratio.Item2))
            {
                return double.NaN;
            }

            if (ratio.Item2 == 0)
            {
                return 0;
            }

            return ratio.Item1 / ratio.Item2;
        }

        public static (int, int) ParseRatioInt(string input, char separator)
        {
            if (string.IsNullOrEmpty(input))
            {
                return (0, 0);
            }

            var ratio = input.Split(separator);
            var first = ratio.Length > 0 ? ParseIntInvariant(ratio[0]) : 0;
            var second = ratio.Length > 1 ? ParseIntInvariant(ratio[1]) : 0;
            return (first, second);
        }

        public static (double, double) ParseRatioDouble(string input, char separator)
        {
            if (string.IsNullOrEmpty(input))
            {
                return (0, 0);
            }

            var ratio = input.Split(separator);
            return (ratio.Length > 0 ? ParseDoubleInvariant(ratio[0]) : 0, ratio.Length > 1 ? ParseDoubleInvariant(ratio[1]) : 0);
        }

        public static double ParseDoubleInvariant(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return 0;
            }

            if (line.Equals("NaN", StringComparison.OrdinalIgnoreCase))
            {
                return double.NaN;
            }

            if (double.TryParse(line, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var result))
            {
                return result;
            }

            return 0;
        }

        public static int ParseIntInvariant(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return 0;
            }

            if (int.TryParse(line, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var result))
            {
                return result;
            }

            return 0;
        }

        public static long ParseLongInvariant(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return 0;
            }

            if (long.TryParse(line, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var result))
            {
                return result;
            }

            return 0;
        }

        public static TimeSpan ParseDuration(string duration)
        {
            if (!string.IsNullOrEmpty(duration))
            {
                var match = DurationRegex.Match(duration);
                if (match.Success)
                {
                    try
                    {
                        // ffmpeg may provide < 3-digit number of milliseconds (omitting trailing zeros), which won't simply parse correctly
                        // e.g. 00:12:02.11 -> 12 minutes 2 seconds and 110 milliseconds
                        var millisecondsPart = match.Groups[4].Value;
                        if (millisecondsPart.Length < 3)
                        {
                            millisecondsPart = millisecondsPart.PadRight(3, '0');
                        }

                        if (!int.TryParse(match.Groups[1].Value, out var hours)) hours = 0;
                        if (!int.TryParse(match.Groups[2].Value, out var minutes)) minutes = 0;
                        if (!int.TryParse(match.Groups[3].Value, out var seconds)) seconds = 0;
                        if (!int.TryParse(millisecondsPart, out var milliseconds)) milliseconds = 0;

                        return new TimeSpan(0, hours, minutes, seconds, milliseconds);
                    }
                    catch
                    {
                        return TimeSpan.Zero;
                    }
                }
                else
                {
                    return TimeSpan.Zero;
                }
            }
            else
            {
                return TimeSpan.Zero;
            }
        }

        public static int ParseRotation(FFProbeStream fFProbeStream)
        {
            var displayMatrixSideData = fFProbeStream.SideData?.Find(item => item.TryGetValue("side_data_type", out var rawSideDataType) && rawSideDataType.ToString() == "Display Matrix");

            if (displayMatrixSideData?.TryGetValue("rotation", out var rawRotation) ?? false)
            {
                var rotationString = rawRotation.ToString();
                if (float.TryParse(rotationString, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var rotationValue))
                {
                    return (int)rotationValue;
                }
            }

            var rotateTag = fFProbeStream.GetRotate() ?? "0";
            if (float.TryParse(rotateTag, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var tagRotationValue))
            {
                return (int)tagRotationValue;
            }

            return 0;
        }

        public static Dictionary<string, bool>? FormatDisposition(Dictionary<string, int>? disposition)
        {
            if (disposition == null)
            {
                return null;
            }

            var result = new Dictionary<string, bool>(disposition.Count, StringComparer.Ordinal);

            foreach (var pair in disposition)
            {
                result.Add(pair.Key, ToBool(pair.Value));
            }

            static bool ToBool(int value) => value switch
            {
                0 => false,
                1 => true,
                _ => throw new ArgumentOutOfRangeException(nameof(value),
                    $"Not expected disposition state value: {value}")
            };

            return result;
        }
    }
}
