using System;
using System.Collections.Generic;
using System.Reflection;
using System.ServiceModel;
using System.ServiceModel.Channels;
using Aquarius.Webclient;
using CommunicationShared;
using CommunicationShared.Dto;
using ServiceStack.Logging;

namespace LocationDeleter
{
    public class LegacyServiceClient : IDisposable
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private IRemoteDataService _aqServiceClient;
        private OperationContextScope _operationContextScope;
        private readonly string _hostName;
        private readonly string _loginUserName;

        private LegacyServiceClient(string hostName, string loginUserName, string password)
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
            _operationContextScope = NewOperationScope(_aqServiceClient, password);
        }

        public void Dispose()
        {
            using (_operationContextScope) { }
            using (_aqServiceClient) { }
        }

        private OperationContextScope NewOperationScope(IRemoteDataService client, string password)
        {
            // ReSharper disable once SuspiciousTypeConversion.Global
            var contextScope = new OperationContextScope((IContextChannel)client);

            try
            {
                var credentials = new UserCredentials
                {
                    UserName = _loginUserName,
                    Password = password
                };

                var header = MessageHeader.CreateHeader(
                    typeof(UserCredentials).Name,
                    UserCredentials.WS_NAMESPACE,
                    credentials,
                    false
                );

                OperationContext.Current.OutgoingMessageHeaders.Add(header);

                return contextScope;
            }
            catch (Exception ex)
            {
                contextScope.Dispose();
                Log.Error("Error creating OperationContextScope", ex);
                throw;
            }
        }

        public static LegacyServiceClient Create(string hostName, string loginUserName, string password)
        {
            return new LegacyServiceClient(hostName, loginUserName, password);
        }

        public int DeleteLocationAndAllContentById(long locationId, bool preventContentDeletion = true)
        {
            return _aqServiceClient.DeleteLocationList(
                new List<long> { locationId },
                preventContentDeletion, // Must be false to delete everything, even though the parameter name 'cascadeDelete' implies otherwise.
                string.Empty);
        }
    }
}
