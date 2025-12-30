using System.Web;

namespace IISFrontGuard.Handler
{
    public class FrontGuardHandler : IHttpHandler
    {
        public bool IsReusable => throw new System.NotImplementedException();

        public void ProcessRequest(HttpContext context)
        {
            throw new System.NotImplementedException();
        }
    }
}