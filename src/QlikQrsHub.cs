#region License
/*
Copyright (c) 2018 Konrad Mattheis und Martin Berthold
Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
 */
#endregion

namespace Q2gHelperQrs
{
    #region Usings
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using Newtonsoft.Json.Serialization;
    using NLog;
    using SerApi;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Security;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading.Tasks;
    using System.Web;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Threading;
    #endregion

    public class QlikQrsHub
    {
        #region Logger
        private static Logger logger = LogManager.GetCurrentClassLogger();
        #endregion

        #region Properties & Variables
        private Uri ConnectUri = null;
        private Cookie ConnectCookie = null;
        private Uri SharedContentUri = null;
        #endregion

        #region Constructor
        public QlikQrsHub(Uri connectUri, Cookie cookie)
        {
            ConnectUri = connectUri;
            ConnectCookie = cookie;
            SharedContentUri = new Uri(ConnectUri, $"{ConnectUri.AbsolutePath}/qrs/sharedcontent");
        }
        #endregion

        #region Privat Methods
        private string GetRandomAlphanumericString(int length)
        {
            var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new Random();
            var result = new string(Enumerable.Repeat(chars, length)
                                              .Select(s => s[random.Next(s.Length)])
                                              .ToArray());
            return result;
        }

        private Uri BuildUriWithKey(Uri uri, string key, string filter, string orderby)
        {
            var uriBuilder = new UriBuilder(uri);
            var query = HttpUtility.ParseQueryString(uriBuilder.Query);
            query["Xrfkey"] = key;

            if (filter != null)
                query["filter"] = filter;

            if (orderby != null)
                query["orderby"] = orderby;

            uriBuilder.Query = query.ToString();
            return uriBuilder.Uri;
        }

        public async Task<string> SendRequestAsync(Uri requestUri, HttpMethod method, ContentData data = null,
                                                    string filter = null, string orderby = null)
        {
            try
            {
                var key = GetRandomAlphanumericString(16);
                var keyRelativeUri = BuildUriWithKey(requestUri, key, filter, orderby);
                logger.Debug($"ConnectUri: {keyRelativeUri}");
                var connectionHandler = new HttpClientHandler();
                connectionHandler.CookieContainer.Add(ConnectUri, ConnectCookie);
                var httpClient = new HttpClient(connectionHandler) { BaseAddress = ConnectUri };
                var request = new HttpRequestMessage(method, keyRelativeUri);
                request.Headers.Add("X-Qlik-Xrfkey", key);
                if (data != null)
                {
                    request.Content = new ByteArrayContent(data.FileData);
                    request.Content.Headers.ContentType = new MediaTypeHeaderValue(data.ContentType);
                }

                var result = httpClient.SendAsync(request).Result;
                if (result.IsSuccessStatusCode)
                {
                    logger.Debug($"Response: {result.StatusCode} - {result.RequestMessage}");
                    return await result.Content.ReadAsStringAsync();
                }

                return null;
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"The method \"{nameof(SendRequestAsync)}\" failed.");
                return null;
            }
        }

        private async Task<HubInfo> UploadFileInternalAsync(HubInfo request, ContentData hubFileData, bool isUpdate = false)
        {
            try
            {
                if (request == null)
                {
                    logger.Debug($"The request is null.");
                    return null;
                }

                logger.Debug($"Upload type {request.Type}");
                var httpMethod = HttpMethod.Post;
                var uriString = SharedContentUri;
                if (isUpdate == true)
                {
                    httpMethod = HttpMethod.Put;
                    uriString = new Uri($"{SharedContentUri.OriginalString}/{request.Id.Value}");
                }

                var settings = new JsonSerializerSettings
                {
                    ContractResolver = new CamelCasePropertyNamesContractResolver(),
                    NullValueHandling = NullValueHandling.Ignore
                };
                var jsonStr = JsonConvert.SerializeObject(request, settings);
                var data = Encoding.UTF8.GetBytes(jsonStr);
                var result = await SendRequestAsync(uriString, httpMethod,
                                                    new ContentData() { ContentType = "application/json", FileData = data });
                var hubInfo = JsonConvert.DeserializeObject<HubInfo>(result);

                //Upload File
                if (hubFileData != null)
                {
                    logger.Debug("Upload content data.");
                    if(isUpdate == false)
                        uriString = new Uri($"{SharedContentUri.OriginalString}/{request.Id.Value}");

                    var newUploadUri = new Uri($"{uriString}/uploadfile?externalpath={hubFileData.ExternalPath}");
                    result = await SendRequestAsync(newUploadUri, HttpMethod.Post, hubFileData);
                }
                else
                    logger.Debug("The content data is empty.");

                return hubInfo;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "File upload was failed.");
                return null;
            }
        }
        #endregion

        #region Public Methods
        public async Task<List<HubInfo>> GetSharedContentAsync(HubSelectRequest request)
        {
            try
            {
                var newUri = new Uri($"{SharedContentUri.OriginalString}/full");
                var result = await SendRequestAsync(newUri, HttpMethod.Get, null, request.Filter, request.OrderBy);
                return JsonConvert.DeserializeObject<List<HubInfo>>(result);
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"The method \"{nameof(GetSharedContentAsync)}\" failed.");
                return null;
            }
        }

        public async Task<int> GetSharedContentCountAsync(HubSelectCountRequest request)
        {
            try
            {
                var newUri = new Uri($"{SharedContentUri.OriginalString}/count");
                var result = await SendRequestAsync(newUri, HttpMethod.Get, null, request.Filter);
                var count = JsonConvert.DeserializeObject<JToken>(result).Value<int>("value");
                logger.Debug($"SharedContentCount: {count}");
                return count;
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"The method \"{nameof(GetSharedContentCountAsync)}\" failed.");
                return -1;
            }
        }

        public async Task<HubInfo> CreateSharedContentAsync(HubCreateRequest request)
        {
            try
            {
                var hubInfo = new HubInfo()
                {
                    Type = "Qlik report",
                    Description = request.Description,
                    Name = request.Name,
                };

                return await UploadFileInternalAsync(hubInfo, request.Data);
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"The method \"{nameof(CreateSharedContentAsync)}\" with hub info failed.");
                return null;
            }
        }

        public async Task<HubInfo> UpdateSharedContentAsync(HubUpdateRequest updateRequest)
        {
            try
            {
                updateRequest.Info.CreatedDate = updateRequest.Info.ModifiedDate;
                return await UploadFileInternalAsync(updateRequest.Info, updateRequest.Data, true);
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"The method \"{nameof(UpdateSharedContentAsync)}\" with hub info failed.");
                return null;
            }
        }

        public async Task<bool> DeleteAllSharedContentAsync()
        {
            try
            {
                var sharedInfos = await GetSharedContentAsync(new HubSelectRequest());
                foreach (var hubInfo in sharedInfos)
                    await DeleteSharedContentAsync(new HubDeleteRequest() { Id = hubInfo.Id.Value });
                return true;
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"The method \"{nameof(DeleteAllSharedContentAsync)}\" failed.");
                return false;
            }
        }

        public async Task<bool> DeleteSharedContentAsync(HubDeleteRequest request)
        {
            try
            {
                var newUri = new Uri($"{SharedContentUri.OriginalString}/{request.Id}");
                var result = await SendRequestAsync(newUri, HttpMethod.Delete, null, null);
                return result != null;
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"The method \"{nameof(DeleteSharedContentAsync)}\" with content name failed.");
                return false;
            }
        }
        #endregion
    }
}