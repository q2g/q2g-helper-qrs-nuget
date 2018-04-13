namespace XUnitQrsTest
{
    #region Usings
    using Q2g.HelperQrs;
    using System;
    using System.IO;
    using System.Net;
    using System.Net.Security;
    using System.Security.Cryptography.X509Certificates;
    using Xunit;
    using QrsTest.Properties;
    using Hjson;
    using Newtonsoft.Json;
    #endregion

    public class TestConfig
    {
        public Uri Server { get; set; }
        public string cookieName { get; set; }
        public string cookieValue { get; set; }
    }

    public class QrsTestClass
    {
        private TestConfig GetTestConfig()
        {
            try
            {
                var configPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..\\..\\..\\config.hjson"));
                var json = HjsonValue.Load(configPath).ToString();
                return JsonConvert.DeserializeObject<TestConfig>(json);
            }
            catch(Exception ex)
            {
                return null;
            }
        }

        private QlikQrsHub GetHubConnection()
        {
            var config = GetTestConfig();
           
            //to allow ssl certificates
            ServicePointManager.ServerCertificateValidationCallback = delegate(
            Object obj, X509Certificate certificate, X509Chain chain, SslPolicyErrors errors)
            {
                return true;
            };
            
            return new QlikQrsHub(config.Server, new Cookie(config.cookieName, config.cookieValue));
        }

        [Fact(DisplayName = "Create Shared Content")]
        public HubInfo CreateSharedContent()
        {
            var request = new HubCreateRequest()
            {
                Name = "Unit Test Doc",
                Description = "Test Description from unit test",
                Data = new ContentData()
                {
                    ContentType = "application/pdf",
                    ExternalPath = "demo.pdf",
                    FileData = Resources.create,
                }
            };

            var qrsHub = GetHubConnection();
            var hubInfo = qrsHub.CreateSharedContentAsync(request).Result;
            Assert.True(hubInfo.Id.HasValue);
            return hubInfo;
        }

        [Fact(DisplayName = "Upload Shared Content")]
        public void UploadSharedContent()
        {
            var info = CreateSharedContent();
            var request = new HubUpdateRequest()
            {
                Info = info,
                Data = new ContentData()
                {
                     ContentType = "application/pdf",
                     ExternalPath = "demo.pdf",
                     FileData = Resources.upload,
                }
            };

            var qrsHub = GetHubConnection();
            var hubInfo = qrsHub.UpdateSharedContentAsync(request).Result;
            Assert.True(hubInfo.Id.HasValue);
        }

        [Fact(DisplayName = "Get Shared Content")]
        public void GetSharedContent()
        {
            var qrsHub = GetHubConnection();
            var hubInfos = qrsHub.GetSharedContentAsync().Result;
            Assert.True(hubInfos.Count > 0);
        }

        [Fact(DisplayName = "Get Count of Shared Content")]
        public void GetCountOfSharedContent()
        {
            var qrsHub = GetHubConnection();
            var result = qrsHub.GetSharedContentCountAsync().Result;
            Assert.True(result > 0);
        }

        [Fact(DisplayName = "Delete Shared Content")]
        public void DeleteSharedContent()
        {
            var info = CreateSharedContent();
            var request = new HubDeleteRequest()
            {
                Id = info.Id.Value,
            };

            var qrsHub = GetHubConnection();
            var result = qrsHub.DeleteSharedContentAsync(request).Result;
            Assert.True(result);
        }

        [Fact(DisplayName = "Delete All Shared Content")]
        public void DeleteAllSharedContent()
        {
            var qrsHub = GetHubConnection();
            var result = qrsHub.DeleteAllSharedContentAsync().Result;
            Assert.True(result);
        }
    }
}