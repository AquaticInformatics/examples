using Aquarius.Samples.Client.ServiceModel;
using ServiceStack;

namespace SamplesPlannedSpecimenInstantiator.PrivateApis
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
