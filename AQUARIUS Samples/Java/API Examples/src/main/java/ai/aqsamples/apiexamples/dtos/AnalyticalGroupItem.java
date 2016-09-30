package ai.aqsamples.apiexamples.dtos;

import org.codehaus.jackson.annotate.JsonIgnoreProperties;

@JsonIgnoreProperties(ignoreUnknown = true)
public class AnalyticalGroupItem {
    private ObservedProperty observedProperty;
    private String holdingTime;

    public ObservedProperty getObservedProperty() {
        return observedProperty;
    }

    public void setObservedProperty(ObservedProperty observedProperty) {
        this.observedProperty = observedProperty;
    }

    public String getHoldingTime() {
        return holdingTime;
    }

    public void setHoldingTime(String holdingTime) {
        this.holdingTime = holdingTime;
    }

    @Override
    public String toString() {
        final StringBuilder sb = new StringBuilder("AnalyticalGroupItem{");
        sb.append("observedProperty=").append(observedProperty.getCustomId());
        sb.append(", holdingTime=").append(holdingTime);
        sb.append('}');
        return sb.toString();
    }
}
