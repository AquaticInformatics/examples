package ai.training;

import java.util.List;
import java.util.UUID;

import ai.training.dtos.ObservedProperty;
import ai.training.dtos.UnitGroup;

public class ObservedPropertyExample {

    public static void main(String[] args) {
        if (args.length != 2) {
            System.out.println("Usage:\n" +
                    "java AnalyticalGroupsImporter <AQ Samples URL> <TOKEN>\n\n" +
                    "For Example:\n" +
                    "java AnalyticalGroupsImporter https://mycompany.aqsamples.net/api/v1/ 054203b73b913a6fe5bc8d9da425dff9");
            return;
        }
        final String sampleUrl = args[0];
        final String token = args[1];

        //Initialize AQUARIUS Sample Rest Client
        AqSamplesClient samplesClient = new AqSamplesClient(sampleUrl, token);

        //Get all observed properties from server
        final List<ObservedProperty> observedProperties = samplesClient.getObservedProperties();

        //Print them on the command line
        observedProperties.forEach(observedProperty -> System.out.println(observedProperty.getCustomId()));

        //Post a new observed property to the samples server
        final List<UnitGroup> unitGroups = samplesClient.getUnitGroups();
        ObservedProperty newObservedProperty = new ObservedProperty();
        newObservedProperty.setCustomId("Chlorophyll A " + UUID.randomUUID());
        newObservedProperty.setDescription("Specific form of chlorophyll used in oxygenic photosynthesis");
        newObservedProperty.setResultType("NUMERIC");
        newObservedProperty.setAnalysisType("BIOLOGICAL");
        newObservedProperty.setUnitGroup(unitGroups.get(0)); //Just give it a random unit group
        final ObservedProperty postedObservedProperty = samplesClient.postObservedProperty(newObservedProperty);
        System.out.println("Posted observed property to server:\n" + postedObservedProperty);
    }
}
