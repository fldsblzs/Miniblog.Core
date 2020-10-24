namespace Miniblog.Core.Options
{
    public class BlogOptions
    {
        public int CommentsCloseAfterDays { get; set; } = 10;

        public PostListView ListView { get; set; } = PostListView.TitlesAndExcerpts;

        public string Owner { get; set; } = "The Owner";

        public int PostsPerPage { get; set; } = 4;
    }
}
