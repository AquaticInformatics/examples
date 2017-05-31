package ai.aqsamples.apiexamples;

import java.util.List;
import java.util.Map;
import java.util.UUID;

import ai.aqsamples.apiexamples.dtos.ObservedProperty;
import ai.aqsamples.apiexamples.dtos.UnitGroup;

public class ObservedPropertyExample {

    public static void main(String[] args) {
        if (args.length != 2) {
            System.out.println("Usage:\n" +
                    "java ObservedPropertyExample <AQ Samples URL> <TOKEN>\n\n" +
                    "For Example:\n" +
                    "java ObservedPropertyExample https://mycompany.aqsamples.com/api/v1/ 054203b73b913a6fe5bc8d9da425dff9");
            return;
        }
        final String sampleUrl = args[0];
        final String token = args[1];

        //Initialize AQUARIUS Sample Rest Client
        AqSamplesClient samplesClient = new AqSamplesClient(sampleUrl, token);

        //Get all observed properties from server
        final Map<String, ObservedProperty> observedProperties = samplesClient.getObservedProperties();

        //Print them on the command line
        observedProperties.values().forEach(observedProperty -> System.out.println(observedProperty.getCustomId()));

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

        //Change an existing observed property
        postedObservedProperty.setDescription("Absorbs most energy from wavelengths of violet-blue and orange-red light");
        final ObservedProperty updatedObservedProperty = samplesClient.putObservedProperty(postedObservedProperty);
        System.out.println("Changed observed property to:\n" + updatedObservedProperty);
    }
}
