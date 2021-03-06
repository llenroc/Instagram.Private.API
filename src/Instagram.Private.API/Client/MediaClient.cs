﻿using Instagram.Private.API.Utils;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Instagram.Private.API.Client.Direct
{
    public class UploadFailed : Exception
    {
        public UploadFailed(string message) : base(message)
        {

        }
    }

    public class MediaConfigurationFailed : Exception
    {
        public MediaConfigurationFailed(string message) : base(message)
        {

        }
    }

    public class UploadResponse
    {
        public string upload_id { get; set; }
        public string status { get; set; }
    }


    public class ConfireMediaResponse
    {
        public Media media { get; set; }
        public string upload_id { get; set; }
        public string status { get; set; }

        public class Media
        {
            public int taken_at { get; set; }
            public long pk { get; set; }
            public string id { get; set; }
            public long device_timestamp { get; set; }
            public int media_type { get; set; }
            public string code { get; set; }
            public string client_cache_key { get; set; }
            public int filter_type { get; set; }
            public Image_Versions2 image_versions2 { get; set; }
            public int original_width { get; set; }
            public int original_height { get; set; }
            public User user { get; set; }
            public string organic_tracking_token { get; set; }
            public bool has_liked { get; set; }
            public bool comment_likes_enabled { get; set; }
            public bool has_more_comments { get; set; }
            public int max_num_visible_preview_comments { get; set; }
            public object[] preview_comments { get; set; }
            public int comment_count { get; set; }
            public Caption caption { get; set; }
            public bool caption_is_edited { get; set; }
            public bool photo_of_you { get; set; }
            public bool can_viewer_save { get; set; }
        }

        public class Image_Versions2
        {
            public Candidate[] candidates { get; set; }
        }

        public class Candidate
        {
            public int width { get; set; }
            public int height { get; set; }
            public string url { get; set; }
        }

        public class User
        {
            public long pk { get; set; }
            public string username { get; set; }
            public string full_name { get; set; }
            public bool is_private { get; set; }
            public string profile_pic_url { get; set; }
            public string profile_pic_id { get; set; }
            public bool has_anonymous_profile_picture { get; set; }
            public bool can_boost_post { get; set; }
            public bool can_see_organic_insights { get; set; }
            public bool show_insights_terms { get; set; }
            public bool is_unpublished { get; set; }
        }

        public class Caption
        {
            public long pk { get; set; }
            public long user_id { get; set; }
            public string text { get; set; }
            public int type { get; set; }
            public int created_at { get; set; }
            public int created_at_utc { get; set; }
            public string content_type { get; set; }
            public string status { get; set; }
            public int bit_flags { get; set; }
            public User user { get; set; }
            public long media_id { get; set; }
        }

    }


    public class PostCommand
    {
        public string Caption { get; set; }
        public OnlinePhoto Photo { get; set; }
    }


    public class MediaClient
    {
        private HttpClientWrapper wrapper;
        private Device device;

        public MediaClient(HttpClientWrapper wrapper, Device device)
        {
            this.wrapper = wrapper;
            this.device = device;
        }

        public async Task Post(PostCommand command)
        {
            var photo = UploadPhoto(command.Photo).Result;

            if (photo.status != "ok")
            {
                throw new UploadFailed(photo.status);
            }

            var configuration = await ConfigurePhoto(photo.upload_id, command.Caption);

            if (configuration.status != "ok")
            {
                throw new MediaConfigurationFailed(configuration.status);
            }
        }

        public async Task<UploadResponse> UploadPhoto(Photo photo)
        {
            var predictedUploadId = (long)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalMilliseconds;
            var payload = new
            {
                _uuid = Guid.NewGuid().ToString(),
                device_id = this.device.Id,
                _csrftoken = wrapper.GetCookieValue("csrftoken"),
                // client_context = Guid.NewGuid().ToString(),
                type = "media",
                image_compression = JsonConvert.SerializeObject(new { lib_name = "jt", lib_version = "1.3.0", quality = "92" }),
                upload_id = predictedUploadId
                //recipient_users = $"[[ {to.Id} ]]"
            };

            var response = wrapper
                .SetResource($"upload/photo/")
                .PostAsMultipartWithImage(payload, photo.Content()).Result
                .Content.ReadAsStringAsync().Result
                .Deserialize<UploadResponse>();

            return response;
        }


        public async Task<SendMessageResponse> ConfigurePhoto(string upload_id, string caption)
        {
            var payload = new
            {
                //_uuid = Guid.NewGuid().ToString(),
                //device_id = this.device.Id,
                //_csrftoken = wrapper.GetCookieValue("csrftoken"),
                //client_context = Guid.NewGuid().ToString(),
                source_type = "4",
                caption = caption,
                upload_id = upload_id,
                device = new { manufacturer = "Huawei", model = "HUAWEI SCL - L03", android_version = "20", android_release = "4.4.4" },
                edits = new
                {
                    crop_original_size = "[800.0,800.0]",
                    crop_center = "[0.0,-0.0]",
                    crop_zoom = "1.0"
                },
                extra = new { source_width = "800", source_height = "800" }
            };

            var response = wrapper
                .SetResource($"media/configure/")
                .PostSigned(payload).Result
                //.PostAsMultipart(payload).Result
                .Content.ReadAsStringAsync().Result
                .Deserialize<SendMessageResponse>();

            return response;
        }

        public async Task<MediaResponse> Get(string id)
        {

            var response = wrapper
                .SetResource($"media/{id}/info/")
                .GetAsync().Result
                .Content.ReadAsStringAsync().Result
                .Deserialize<MediaResponse>();

            return response;
        }

        public async Task<MediaResponse> Get(Uri uri)
        {
            var oembed = await Oembed(uri);
            return await Get(oembed.media_id);
        }



        public async Task<MediaResponse> Delete(string id)
        {
            var payload = new
            {
                _uuid = Guid.NewGuid().ToString(),
                device_id = this.device.Id,
                _csrftoken = wrapper.GetCookieValue("csrftoken"),
                media_id = id,
            };

            var response = wrapper
                .SetResource($"media/{id}/delete/?media_type=PHOTO ")
                .PostSigned(payload).Result
                .Content.ReadAsStringAsync().Result
                .Deserialize<MediaResponse>();

            return response;
        }

        public async Task<MediaResponse> Delete(Uri uri)
        {
            var oembed = await Oembed(uri);
            return await Delete(oembed.media_id);
        }

        protected async Task<OembedResponse> Oembed(Uri uri)
        {
            var res = new HttpClientWrapper()
                    .SetBaseAddress($"https://api.instagram.com/oembed?url={uri}")
                    .GetAsync().Result.Content.ReadAsStringAsync().Result
                    .Deserialize<OembedResponse>();
            return res;
        }
    }


    public class MediaResponse
    {
        public Item[] items { get; set; }
        public int num_results { get; set; }
        public bool more_available { get; set; }
        public bool auto_load_more_enabled { get; set; }
        public string status { get; set; }


        public class Item
        {
            public int taken_at { get; set; }
            public long pk { get; set; }
            public string id { get; set; }
            public long device_timestamp { get; set; }
            public int media_type { get; set; }
            public string code { get; set; }
            public string client_cache_key { get; set; }
            public int filter_type { get; set; }
            public Image_Versions2 image_versions2 { get; set; }
            public int original_width { get; set; }
            public int original_height { get; set; }
            public User user { get; set; }
            public string organic_tracking_token { get; set; }
            public int like_count { get; set; }
            public string[] top_likers { get; set; }
            public bool has_liked { get; set; }
            public bool comment_likes_enabled { get; set; }
            public bool has_more_comments { get; set; }
            public int max_num_visible_preview_comments { get; set; }
            public object[] preview_comments { get; set; }
            public int comment_count { get; set; }
            public Caption caption { get; set; }
            public bool caption_is_edited { get; set; }
            public bool photo_of_you { get; set; }
            public bool can_viewer_save { get; set; }
        }

        public class Image_Versions2
        {
            public Candidate[] candidates { get; set; }
        }

        public class Candidate
        {
            public int width { get; set; }
            public int height { get; set; }
            public string url { get; set; }
        }

        public class User
        {
            public long pk { get; set; }
            public string username { get; set; }
            public string full_name { get; set; }
            public bool is_private { get; set; }
            public string profile_pic_url { get; set; }
            public string profile_pic_id { get; set; }
            public bool is_verified { get; set; }
            public bool has_anonymous_profile_picture { get; set; }
            public bool can_boost_post { get; set; }
            public bool can_see_organic_insights { get; set; }
            public bool show_insights_terms { get; set; }
            public bool is_unpublished { get; set; }
        }

        public class Caption
        {
            public long pk { get; set; }
            public long user_id { get; set; }
            public string text { get; set; }
            public int type { get; set; }
            public int created_at { get; set; }
            public int created_at_utc { get; set; }
            public string content_type { get; set; }
            public string status { get; set; }
            public int bit_flags { get; set; }
            public User user { get; set; }
            public long media_id { get; set; }
        }
    }




    public class OembedResponse
    {
        public string version { get; set; }
        public string title { get; set; }
        public string author_name { get; set; }
        public string author_url { get; set; }
        public long author_id { get; set; }
        public string media_id { get; set; }
        public string provider_name { get; set; }
        public string provider_url { get; set; }
        public string type { get; set; }
        public int width { get; set; }
        public object height { get; set; }
        public string html { get; set; }
        public string thumbnail_url { get; set; }
        public int thumbnail_width { get; set; }
        public int thumbnail_height { get; set; }
    }

}
