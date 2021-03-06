﻿namespace Q2g.HelperQrs
{
    #region Usings
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Security;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading.Tasks;
    using System.Web;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using NLog;
    #endregion

    public class QlikQrsHub
    {
        #region Logger
        private readonly static Logger logger = LogManager.GetCurrentClassLogger();
        #endregion

        #region Properties & Variables
        private readonly bool UseCertificate = false;
        private readonly CertAuthentication CertAuth = null;
        private readonly Cookie ConnectCookie = null;

        public Uri ConnectUri { get; private set; }
        public Func<HttpRequestMessage, X509Certificate2, X509Chain, SslPolicyErrors, bool> ServerCertificateValidationCallback { get; set; }
        #endregion

        #region Constructor
        public QlikQrsHub(Uri connectUri, Cookie cookie)
        {
            UseCertificate = false;
            ConnectUri = connectUri;
            ConnectCookie = cookie;
        }

        public QlikQrsHub(Uri connectUri, CertAuthentication certAuth)
        {
            UseCertificate = true;
            ConnectUri = connectUri;
            CertAuth = certAuth;
        }
        #endregion

        #region Privat Methods
        private string GetXrfKey(int length = 16)
        {
            var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqerstuvwxyz0123456789";
            var random = new Random();
            var result = new string(Enumerable.Repeat(chars, length)
                                              .Select(s => s[random.Next(s.Length)])
                                              .ToArray());
            return result;
        }

        private Uri BuildUriWithKey(string pathAndQuery, string key, string filter, string orderby, bool privileges)
        {
            var uriBuilder = new UriBuilder($"{ConnectUri.AbsoluteUri.TrimEnd('/')}/qrs/{pathAndQuery}");

            var query = HttpUtility.ParseQueryString(uriBuilder.Query);
            query["Xrfkey"] = key;

            if (filter != null)
                query["filter"] = filter;

            if (orderby != null)
                query["orderby"] = orderby;

            if (privileges)
                query["privileges"] = privileges.ToString().ToLowerInvariant();

            uriBuilder.Query = query.ToString();
            return uriBuilder.Uri;
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
                var pathAndQuery = "sharedcontent";
                if (isUpdate == true)
                {
                    httpMethod = HttpMethod.Put;
                    pathAndQuery += $"/{request.Id.Value}";
                }

                var jsonStr = JsonConvert.SerializeObject(request);
                var data = Encoding.UTF8.GetBytes(jsonStr);
                var result = await SendRequestAsync(pathAndQuery, httpMethod,
                                                    new ContentData() { ContentType = "application/json", FileData = data });
                var hubInfo = JsonConvert.DeserializeObject<HubInfo>(result);

                //Upload File
                if (hubFileData != null)
                {
                    logger.Debug("Upload content data.");
                    if (isUpdate == false)
                        pathAndQuery += $"/{hubInfo.Id.Value}";

                    pathAndQuery = $"{pathAndQuery}/uploadfile?externalpath={hubFileData.ExternalPath}";
                    result = await SendRequestAsync(pathAndQuery, HttpMethod.Post, hubFileData);
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
        public async Task<string> SendRequestAsync(string pathAndQuery, HttpMethod method, ContentData data = null,
                                                   string filter = null, string orderby = null, bool privileges = false)
        {
            try
            {
                var key = QrsUtilities.GetXrfKey(16);
                var keyRelativeUri = BuildUriWithKey(pathAndQuery, key, filter, orderby, privileges);
                logger.Debug($"ConnectUri: {keyRelativeUri}");
                var connectionHandler = new HttpClientHandler();
                if (UseCertificate)
                {
                    if (!CertAuth.Validate())
                        throw new Exception($"Certificate authenification failed. (Key='{CertAuth?.Key}' | Value='{CertAuth?.Value}' | Certificate='{CertAuth?.Certificate?.Thumbprint}')");
                    connectionHandler.ClientCertificates.Add(CertAuth.Certificate);
                }
                else
                    connectionHandler.CookieContainer.Add(ConnectUri, ConnectCookie);
                if (this.ServerCertificateValidationCallback != null)
                    connectionHandler.ServerCertificateCustomValidationCallback = ServerCertificateValidationCallback;
                else
                    connectionHandler.ServerCertificateCustomValidationCallback = (sender, certificate, chain, sslPolicyErrors) =>
                    {
                        var callback = ServicePointManager.ServerCertificateValidationCallback;
                        if (callback != null)
                            return callback(sender, certificate, chain, sslPolicyErrors);
                        return false;
                    };

                var httpClient = new HttpClient(connectionHandler) { BaseAddress = ConnectUri };
                var request = new HttpRequestMessage(method, keyRelativeUri);
                if (UseCertificate)
                    request.Headers.Add(CertAuth.Key, CertAuth.Value);
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
                else
                {
                    logger.Error($"Send request failed {result}");
                }

                return null;
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"The method \"{nameof(SendRequestAsync)}\" failed.");
                return null;
            }
        }

        public async Task<List<HubInfo>> GetSharedContentAsync(HubSelectRequest request = null)
        {
            try
            {
                if (request == null)
                    request = new HubSelectRequest();

                var result = await SendRequestAsync("sharedcontent/full", HttpMethod.Get, null, request.Filter, request.OrderBy);
                return JsonConvert.DeserializeObject<List<HubInfo>>(result);
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"The method \"{nameof(GetSharedContentAsync)}\" failed.");
                return null;
            }
        }

        public async Task<int> GetSharedContentCountAsync(HubSelectCountRequest request = null)
        {
            try
            {
                if (request == null)
                    request = new HubSelectRequest();

                var result = await SendRequestAsync("sharedcontent/count", HttpMethod.Get, null, request.Filter);
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
                    Type = request.ReportType,
                    Description = request.Description,
                    Name = request.Name,
                    MetaData = new List<MetaData>()
                    {
                        new MetaData()
                        {
                            Id = Guid.NewGuid().ToString(),
                            Key = "ser-type",
                            Value = "report",
                        }
                    }
                };

                if (request.Tags != null && request.Tags.Count > 0)
                {
                    var tagJson = SendRequestAsync("tag/full", HttpMethod.Get).Result;
                    var tagList = JsonConvert.DeserializeObject<List<Tag>>(tagJson);
                    hubInfo.Tags = new List<Tag>();
                    foreach (var tag in request.Tags)
                    {
                        var qlikTag = tagList.FirstOrDefault(t => t.Name == tag.Name);
                        if (qlikTag != null)
                            hubInfo.Tags.Add(qlikTag);
                    }
                }

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
                updateRequest.Info.ModifiedDate = DateTime.Now;
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
                logger.Error(ex, $"The method \"{nameof(DeleteAllSharedContentAsync)}\" was failed.");
                return false;
            }
        }

        public async Task<bool> DeleteSharedContentAsync(HubDeleteRequest request)
        {
            try
            {
                var result = await SendRequestAsync($"sharedcontent/{request.Id}", HttpMethod.Delete, null, null);
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