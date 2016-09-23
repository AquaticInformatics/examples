package ai.training.dtos;

import org.codehaus.jackson.annotate.JsonIgnoreProperties;

@JsonIgnoreProperties(ignoreUnknown = true)
public class ObservedProperty {

    private String id;
    private String customId;
    private String description;
    private String resultType;
    private String analysisType;
    private UnitGroup unitGroup;

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

    public String getDescription() {
        return description;
    }

    public void setDescription(String description) {
        this.description = description;
    }

    public String getResultType() {
        return resultType;
    }

    public void setResultType(String resultType) {
        this.resultType = resultType;
    }

    public String getAnalysisType() {
        return analysisType;
    }

    public void setAnalysisType(String analysisType) {
        this.analysisType = analysisType;
    }

    public UnitGroup getUnitGroup() {
        return unitGroup;
    }

    public void setUnitGroup(UnitGroup unitGroup) {
        this.unitGroup = unitGroup;
    }

    @Override
    public String toString() {
        final StringBuilder sb = new StringBuilder("ObservedProperty{");
        sb.append("id=").append(id);
        sb.append(", customId=").append(customId);
        sb.append(", description=").append(description);
        sb.append(", resultType=").append(resultType);
        sb.append(", analysisType=").append(analysisType);
        sb.append('}');
        return sb.toString();
    }
}
