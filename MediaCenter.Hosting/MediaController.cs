using MediaCenter.Hosting.Models;
using System.Collections.Generic;
using System.IO;
using System.Web;
using System.Web.Http;

namespace MediaCenter.Hosting
{
    public class MediaController : ApiController
    {
        // GET api/<controller>
        public IEnumerable<Video> Get()
        {
            var videos = new List<Video>();

            var DvdPath = @"C:\MediaStreamer\Content\DVDs";
            var MainDirectory = Directory.GetDirectories(DvdPath);

            foreach (var folder in MainDirectory)
            {
                var directory = new DirectoryInfo(folder);
                var name = directory.Name;

                videos.Add(new Video()
                {
                    Name = name,
                    CoverPhoto = "/" + name + "/photo.jpg",
                    MovieURL = "/" + name + "/movie.mp4"
                });
            }

            return videos;
        }
    }
}
