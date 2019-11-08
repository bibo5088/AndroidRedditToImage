using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Android.App;
using Android.OS;
using Android.Support.V7.App;
using Android.Content;
using System.Net.Http;
using Android.Graphics;
using Android.Support.V4.Content;
using Android.Util;
using Android.Webkit;
using Java.IO;
using Newtonsoft.Json.Linq;
using Xamarin.Android.Net;
using Uri = Android.Net.Uri;


namespace AndroidRedditToImage
{
    [Activity(Label = "@string/app_name", MainLauncher = true)]
    [IntentFilter(new[] {Intent.ActionSend}, Categories = new[] {Intent.CategoryDefault}, DataMimeType = "text/plain")]
    public class MainActivity : AppCompatActivity
    {
        private static readonly HttpClient HttpClient = new HttpClient(new AndroidClientHandler());

        private static readonly Regex RedditUrlRegex =
            new Regex(@"^https?:\/\/.*\.reddit.com\/r\/.+\/comments\/[^\/]+", RegexOptions.Compiled);

        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);

            if (TryGetRedditUrl(out var redditUrl))
            {
                var imageUrl = GetImageUrl(redditUrl).Result;
                var localImageUri = DownloadImage(imageUrl);
                SendImage(localImageUri);
            }

            Finish();
        }

        private bool TryGetRedditUrl(out string redditUrl)
        {
            if (Intent.Action == Intent.ActionSend)
            {
                var url = Intent.Extras.GetString(Intent.ExtraText);
                var match = RedditUrlRegex.Match(url);

                if (match.Success)
                {
                    redditUrl = match.Captures[0].Value;
                    return true;
                }
            }

            redditUrl = null;
            return false;
        }

        private async Task<string> GetImageUrl(string postUrl)
        {
            var res = HttpClient.GetAsync($"{postUrl}.json").Result;

            res.EnsureSuccessStatusCode();

            var top = JArray.Parse(await res.Content.ReadAsStringAsync());
            res.Dispose();
            /* 
            [ 
               { 
                  "data":{ 
                     "children":[ 
                        { 
                           "data":{ 
                              "url":"http://..."
                           }
                        }
                     ]
                  }
               }
             */
            return top[0]["data"]["children"][0]["data"]["url"].Value<string>();
        }

        private Uri DownloadImage(string url)
        {
            var res = HttpClient.GetAsync(url).Result;
            if (!res.Content.Headers.ContentType.MediaType.StartsWith("image")) throw new Exception("Is not an image");

            res.EnsureSuccessStatusCode();

            var extension = MimeTypeMap.Singleton.GetExtensionFromMimeType(res.Content.Headers.ContentType.MediaType);
            var path = $"{GetExternalCacheDirs()[0].AbsolutePath}{File.Separator}reddit_to_image_{Guid.NewGuid()}.{extension}";
            using (var os = new System.IO.FileStream(path, System.IO.FileMode.Create))
            {
                res.Content.CopyToAsync(os).Wait();
            }

            res.Dispose();
            return FileProvider.GetUriForFile(this, Application.Context.PackageName + ".provider",
                new File(path));
        }

        private void SendImage(Uri imageUri)
        {
            var sharingIntent = new Intent();
            sharingIntent.SetAction(Intent.ActionSend);
            sharingIntent.SetType("image/*");
            sharingIntent.PutExtra(Intent.ExtraStream, imageUri);
            sharingIntent.AddFlags(ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantPersistableUriPermission);
            StartActivity(Intent.CreateChooser(sharingIntent, "Image"));
        }
    }
}