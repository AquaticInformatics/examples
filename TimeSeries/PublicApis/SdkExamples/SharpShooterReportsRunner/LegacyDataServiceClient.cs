using System;
using Aquarius.Webclient;
using CommunicationShared;

namespace SharpShooterReportsRunner
{
    public class LegacyDataServiceClient : IDisposable
    {
        private IRemoteDataService _aqServiceClient;
        private readonly string _hostName;
        private readonly string _loginUserName;

        public static LegacyDataServiceClient Create(string hostName, string loginUserName, string password)
        {
            return new LegacyDataServiceClient(hostName, loginUserName, password);
        }

        private LegacyDataServiceClient(string hostName, string loginUserName, string password)
        {
            ThrowIfNotSpecified(hostName, nameof(hostName));
            ThrowIfNotSpecified(loginUserName, nameof(loginUserName));
            ThrowIfNotSpecified(password, nameof(password));

            _hostName = hostName;
            _loginUserName = loginUserName;

            CreateConnectedAquariusDataServiceClient(password);
        }

        private void ThrowIfNotSpecified(string stringValue, string paramName)
        {
            if (!string.IsNullOrWhiteSpace(stringValue))
                return;

            throw new ArgumentNullException($"{paramName} is null or empty.");
        }

        private void CreateConnectedAquariusDataServiceClient(string password)
        {
            _aqServiceClient = AQWSFactory.NewADSClient(_hostName, _loginUserName, password);
        }

        public void Dispose()
        {
            using (_aqServiceClient) { }
        }

        public byte[] GetRatingModelAop(long aopId)
        {
            const string ratingModelTable = "AQAtom_HYDROML";
            return _aqServiceClient
                .GetAtom(ratingModelTable, aopId);
        }

        public string GetSiteVisitData(string locationIdentifier)
        {
            return _aqServiceClient.GetSiteVisitData(locationIdentifier, null, null, null);
        }
    }
}
