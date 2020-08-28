using Aquarius.Samples.Client.ServiceModel;
using ServiceStack;

namespace SamplesObservationExporter.PrivateApis
{
    [Route("/v1/usertokens")]
    public class GetUserTokens : IReturn<UserTokensResponse>
    {
    }

    public class UserTokensResponse
    {
        public User User { get; set; }
    }
}
