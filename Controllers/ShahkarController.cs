using JsonWebToken;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using ShahkarAPI.Models;
using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace ShahkarAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ShahkarController : ControllerBase
    {

        // گام اول:
        // اطلاعات خواسته در "باکس یک" را کامل کنید

        #region باکس یک
        // اطلاعات خواسته شده در این بخش را براساس فایل اکسل و برگه کاغذی دریافتی کامل کنید
        // اطلاعات اكسل شاهكار
        private const string _username = "";
        private const string _password = "";
        private const string _auth = "";
        private const string _pid = "";

        //اطلاعات کاربری سامانه شاهکار 
        //روی کاغذ 
        // Provider code:
        private const string _providerCode = "";
        // Username:
        private const string _usernamePaper = "";
        // Password:
        private const string _passwordPaper = "";
        #endregion

        // گام دوم:
        // گام دوم: کلید عمومی در متد زیر قرار دهید
        private const string publicKeyStringShahkar = @"-----BEGIN PUBLIC KEY-----   
-----END PUBLIC KEY-----";

        //متغیرهای برنامه
        private readonly string _basicAuthorization;
        private TokenInfo? _tokenGenerated;

        private readonly ILogger<TokenInfo> _logger;

        public ShahkarController(ILogger<TokenInfo> logger)
        {
            _logger = logger;
            _basicAuthorization = GenerateBasicAuthorization(_usernamePaper, _passwordPaper);
        }

        /// <summary>
        /// تطبیق کد ملی و شماره تلفن همراه
        /// </summary>
        /// <param name="nationalCode"></param>
        /// <param name="mobile"></param>
        /// <returns></returns>
        [HttpGet("ShahkarMatchingAsync")]
        public async Task<ShahkarResult<string>> ShahkarMatchingAsync(string nationalCode, string mobile)
        {
            _tokenGenerated = await GenerateToken();

            ShahkarResult<string> shahkarResult = new ShahkarResult<string>();

            // در صورتی که توکنی تولید نشد
            //اینجا می توانید خطایی را بازگردانید
            if (_tokenGenerated == null)
            {
                shahkarResult.IsSuccess = 0;
                shahkarResult.Data = string.Empty;
                return shahkarResult;
            }

            TimeSpan t = DateTime.UtcNow - new DateTime(1970, 1, 1);
            int secondsSinceEpoch = (int)t.TotalSeconds;

            string nationalCodeEncrypted = GetEncryptedToken(nationalCode, secondsSinceEpoch);
            string mobileEncrypted = GetEncryptedToken(mobile, secondsSinceEpoch);

            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            string url = "https://op1.pgsb.ir/api/client/apim/v1/shahkaar/gwsh/serviceIDmatchingencrypted";

            try
            {
                using (HttpClient client = new HttpClient())
                {
                    string DateString = DateTime.Now.ToString("yyyyMMdd");
                    string TimeString = DateTime.Now.ToString("HHmmss");
                    string RequestId = _providerCode + DateString + TimeString + "000000";

                    client.DefaultRequestHeaders.Add("pid", _pid);
                    client.DefaultRequestHeaders.Add("Authorization", _tokenGenerated.token_type + " " + _tokenGenerated.access_token);
                    client.DefaultRequestHeaders.Add("basicAuthorization", _basicAuthorization);

                    // Create a JSON string with your data
                    string jsonData = "{\"serviceType\":\"2\",\"identificationType\":\"0\",\"identificationNo\":\"" + nationalCodeEncrypted
                        + "\",\"requestId\":\"" + RequestId + "\",\"serviceNumber\":\"" + mobileEncrypted + "\"}";

                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    var content = new StringContent(jsonData, Encoding.UTF8, "application/json");

                    HttpResponseMessage response = await client.PostAsync(url, content);

                    string responseContent = await response.Content.ReadAsStringAsync();

                    //output
                    shahkarResult.IsSuccess = 1;
                    shahkarResult.Data = responseContent;

                    return shahkarResult;
                }
            }
            catch (Exception)
            {
                shahkarResult.IsSuccess = 0;
                //Generate your exception
                throw;
            }
        }

        /// <summary>
        /// تولید basicAuthorization
        /// </summary>
        /// <returns></returns>
        [NonAction]
        public string GenerateBasicAuthorization(string username, string password)
        {
            var TokenBase64 = Encoding.UTF8.GetBytes(username + ":" + password);
            string token = Convert.ToBase64String(TokenBase64);
            return "Basic " + token;
        }

        /// <summary>
        /// ساختن توکن
        /// </summary>
        /// <returns></returns>
        [NonAction]
        public async Task<TokenInfo?> GenerateToken()
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    // تنظیم مسیر توکن
                    string TokenEndpoint = "https://op1.pgsb.ir/oauth/token";

                    // تنظیم نوع محتوا
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    // تنظیم اطلاعات تأیید هویت برای ارسال در هدر
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", _auth);

                    var formData = new List<KeyValuePair<string, string>>
                    {
                        new KeyValuePair<string, string>("grant_type", "password"),
                        new KeyValuePair<string, string>("username", _username),
                        new KeyValuePair<string, string>("password", _password)
                    };

                    // ایجاد محتوای فرم و ارسال درخواست POST
                    var content = new FormUrlEncodedContent(formData);
                    var response = await client.PostAsync(TokenEndpoint, content);

                    // بررسی موفقیت درخواست
                    if (response.IsSuccessStatusCode)
                    {
                        var responseContent = await response.Content.ReadAsStringAsync();
                        var tokenInfo = JsonConvert.DeserializeObject<TokenInfo>(responseContent);
                        return tokenInfo;
                    }
                    else
                    {
                        // در صورت خطا، می‌توانید با خطاهای احتمالی برخورد کنید.
                        // مثلاً:
                        if (response.StatusCode == HttpStatusCode.Unauthorized)
                        {
                            // عملیات تأیید هویت ناموفق بود
                        }
                        else
                        {
                            // خطای دیگر
                        }
                        return null;
                    }
                }
            }
            catch (Exception)
            {
                //Generate your exception
                throw;
            }
        }

        /// <summary>
        /// رمزنگاری داده با کلید عمومی
        /// </summary>
        /// <param name="inputData">داده مورد نظر</param>
        /// <param name="inputIat">IAT</param>
        /// <returns></returns>
        private static string GetEncryptedToken(string inputData, long inputIat)
        {
            var asymmetricJwkKey = Jwk.FromPem(publicKeyStringShahkar);

            var payload = new
            {
                data = inputData,
                iat = inputIat
            };

            string jsonPayload = JsonConvert.SerializeObject(payload);

            var asyDescriptorPlainText = new PlaintextJweDescriptor(asymmetricJwkKey,
                KeyManagementAlgorithm.EcdhEsA256KW, JsonWebToken.EncryptionAlgorithm.A256Gcm)
            {
                Payload = jsonPayload
            };

            var jwtWriter = new JwtWriter();
            var encryptedToken = jwtWriter.WriteTokenString(asyDescriptorPlainText);

            return encryptedToken;
        }
    }
}
