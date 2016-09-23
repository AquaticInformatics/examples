package ai.training.dtos;

import org.codehaus.jackson.annotate.JsonIgnoreProperties;

@JsonIgnoreProperties(ignoreUnknown = true)
public class UnitGroup {

    private String id;
    private String customId;
    private boolean supportsConversion;

    public String getId() {
        return id;
    }

    public void setId(String id) {
        this.id = id;
    }

    public String getCustomId() {
        return customId;
    }

    public void setCustomId(String customId) {
        this.customId = customId;
    }

    public boolean isSupportsConversion() {
        return supportsConversion;
    }

    public void setSupportsConversion(boolean supportsConversion) {
        this.supportsConversion = supportsConversion;
    }
}
