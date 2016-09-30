package ai.aqsamples.apiexamples.dtos;

import java.util.List;

import org.codehaus.jackson.annotate.JsonIgnoreProperties;

@JsonIgnoreProperties(ignoreUnknown = true)
public class AnalyticalGroup {
    private String id;
    private String customId;
    private String name;
    private String description;
    private String type;
    private List<AnalyticalGroupItem> analyticalGroupItems;

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

    public String getName() {
        return name;
    }

    public void setName(String name) {
        this.name = name;
    }

    public String getDescription() {
        return description;
    }

    public void setDescription(String description) {
        this.description = description;
    }

    public String getType() {
        return type;
    }

    public void setType(String type) {
        this.type = type;
    }

    public List<AnalyticalGroupItem> getAnalyticalGroupItems() {
        return analyticalGroupItems;
    }

    public void setAnalyticalGroupItems(List<AnalyticalGroupItem> analyticalGroupItems) {
        this.analyticalGroupItems = analyticalGroupItems;
    }

    @Override
    public String toString() {
        final StringBuilder sb = new StringBuilder("AnalyticalGroup{");
        sb.append("id=").append(id);
        sb.append(", name=").append(name);
        sb.append(", description=").append(description);
        sb.append(", type=").append(type);
        sb.append(", analyticalGroupItems=").append(analyticalGroupItems);
        sb.append('}');
        return sb.toString();
    }
}
