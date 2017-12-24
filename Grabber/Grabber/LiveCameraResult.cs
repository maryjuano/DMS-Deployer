using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Grabber
{
    public class LiveCameraResult
    {
        public Microsoft.ProjectOxford.Face.Contract.Face[] Faces { get; set; } = null;
        public Microsoft.ProjectOxford.Common.Contract.EmotionScores[] EmotionScores { get; set; } = null;
        public Dictionary<string, string> IdentifiedFaces { get; set; } = new Dictionary<string, string>();
    }
}
