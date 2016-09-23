package ai.training;

import java.util.List;
import javax.ws.rs.client.Client;
import javax.ws.rs.client.ClientBuilder;
import javax.ws.rs.client.Entity;
import javax.ws.rs.core.GenericType;
import javax.ws.rs.core.MediaType;
import javax.ws.rs.core.Response;

import ai.training.dtos.ObservedProperty;
import ai.training.dtos.UnitGroup;

public class AqSamplesClient {

    private static final String OBSERVED_PROPERTIES_PATH = "observedproperties";
    private static final String UNIT_GROUPS_PATH = "unitgroups";
    private static final String TOKEN_PARAM = "token";
    private final String baseUri;
    private final String token;

    public AqSamplesClient(String baseUri, String token) {
        this.token = token;
        this.baseUri = baseUri;
    }

    public List<UnitGroup> getUnitGroups() {
        Client client = ClientBuilder.newClient();
        final Response response = client.target(createUrl(UNIT_GROUPS_PATH)).request().get();
        throwExceptionIfError(response);
        return response.readEntity(new GenericType<List<UnitGroup>>() { });
    }

    public List<ObservedProperty> getObservedProperties() {
        Client client = ClientBuilder.newClient();
        final Response response = client.target(createUrl(OBSERVED_PROPERTIES_PATH)).request().get();
        throwExceptionIfError(response);
        return response.readEntity(new GenericType<List<ObservedProperty>>() { });
    }

    public ObservedProperty postObservedProperty(ObservedProperty observedProperty) {
        Client client = ClientBuilder.newClient();
        Response response = client.target(createUrl(OBSERVED_PROPERTIES_PATH))
                .request()
                .accept(MediaType.APPLICATION_JSON_TYPE)
                .post(Entity.entity(observedProperty, MediaType.APPLICATION_JSON_TYPE));
        throwExceptionIfError(response);
        return response.readEntity(ObservedProperty.class);
    }

    private void throwExceptionIfError(Response response) {
        if (response.getStatus() != 200) {
            throw new RuntimeException("Status Code: " + response.getStatus() + "\n" + response.readEntity(String.class));
        }
    }

    private String createUrl(String path) {
        return baseUri + path + "?" + TOKEN_PARAM + "=" + token;
    }
}
