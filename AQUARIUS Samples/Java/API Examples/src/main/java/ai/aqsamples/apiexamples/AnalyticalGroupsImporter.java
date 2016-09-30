package ai.aqsamples.apiexamples;

import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.Paths;
import java.util.LinkedList;
import java.util.List;
import java.util.Map;
import java.util.stream.Stream;

import ai.aqsamples.apiexamples.dtos.AnalyticalGroup;
import ai.aqsamples.apiexamples.dtos.AnalyticalGroupItem;
import ai.aqsamples.apiexamples.dtos.ObservedProperty;

public class AnalyticalGroupsImporter {

    public static void main(String[] args) {
        if (args.length != 3) {
            System.out.println("Usage:\n" +
                    "java AnalyticalGroupsImporter <AQ Samples URL> <TOKEN> <FILE>\n\n" +
                    "For Example:\n" +
                    "java AnalyticalGroupsImporter https://mycompany.aqsamples.com/api/v1/ 054203b73b913a6fe5bc8d9da425dff9 " +
                    "src/main/resources/AnalyticalGroups.csv");
            return;
        }
        final String sampleUrl = args[0];
        final String token = args[1];
        final String analyticalGroupsFile = args[2];
        final Path analyticalGroupsFilePath = Paths.get(analyticalGroupsFile);

        //Initialize AQUARIUS Sample Rest Client
        AqSamplesClient samplesClient = new AqSamplesClient(sampleUrl, token);

        //Get all observed properties from server
        Map<String, ObservedProperty> observedPropertyCustomIdToObject = samplesClient.getObservedProperties();

        try (Stream<String> stream = Files.lines(analyticalGroupsFilePath)) {

            stream.forEach(line -> {
                String analyticalGroupName = extractAnalyticalGroupName(line);
                List<String> observedPropertyCustomIds = extractObservedPropertyCustomIds(line);

                //Create Analytical Group from line in csv
                AnalyticalGroup analyticalGroup = new AnalyticalGroup();
                analyticalGroup.setName(analyticalGroupName);
                analyticalGroup.setAnalyticalGroupItems(new LinkedList<>());
                analyticalGroup.setType("KNOWN");
                observedPropertyCustomIds.forEach(observedPropertyCustomId -> {
                    final AnalyticalGroupItem analyticalGroupItem = new AnalyticalGroupItem();
                    analyticalGroupItem.setObservedProperty(observedPropertyCustomIdToObject.get(observedPropertyCustomId));
                    analyticalGroup.getAnalyticalGroupItems().add(analyticalGroupItem);
                });

                //Post Analytical Group to Samples Server
                final AnalyticalGroup postedGroup = samplesClient.postAnalyticalGroup(analyticalGroup);
                System.out.println("Posted Group:" + postedGroup);
            });

        } catch (IOException e) {
            e.printStackTrace();
        }
    }

    private static List<String> extractObservedPropertyCustomIds(String csvLine) {
        final String[] parts = csvLine.split(",");
        List<String> observedPropertyIds = new LinkedList<>();
        for (int i = 1; i < parts.length; i++) {
            observedPropertyIds.add(parts[i]);
        }
        return observedPropertyIds;
    }

    private static String extractAnalyticalGroupName(String csvLine) {
        return csvLine.split(",")[0];
    }
}
