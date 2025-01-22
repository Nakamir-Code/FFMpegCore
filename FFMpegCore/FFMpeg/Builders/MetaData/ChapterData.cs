namespace FFMpegCore.Builders.MetaData
{
    public class ChapterData
    {
        public string Title { get; set; }
        public TimeSpan Start { get; set; }
        public TimeSpan End { get; set; }

        public TimeSpan Duration => End - Start;

        public ChapterData(string title, TimeSpan start, TimeSpan end)
        {
            Title = title;
            Start = start;
            End = end;
        }
    }
}
