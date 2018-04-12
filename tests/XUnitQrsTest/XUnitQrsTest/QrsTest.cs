namespace XUnitQrsTest
{
    #region Usings
    using Q2gHelperQrs;
    using System;
    using System.IO;
    using System.Net;
    using System.Net.Security;
    using System.Security.Cryptography.X509Certificates;
    using Xunit;
    using XUnitQrsTest.Properties;
    #endregion

    public class QrsTest
    {
        private QlikQrsHub GetHubConnection()
        {
            //change connection information here.
            //for example use postman to generate a cookie
            //this test use a configured jwt session with qlik sense server
            //https://localhost/ser/sense/app
            var uri = new Uri("https://localhost/ser");
            var cookie = new Cookie("X-Qlik-Session-ser", "31154527-1739-4bf1-bff5-9e107d076f13");

            ServicePointManager.ServerCertificateValidationCallback = delegate(
            Object obj, X509Certificate certificate, X509Chain chain, SslPolicyErrors errors)
            {
                return true;
            };

            return new QlikQrsHub(uri, cookie);
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