## ------------------------------------------------------------------------

library(zoo)
library(zip)
library(RCurl)
library(jsonlite)

rm(list = ls())

#REVERT
setwd("C:/Users/rafael.banchs/Downloads/WDTF_Donald_Docs/scripts/rafa testing/DPIPWE.20180816.wdtfExport_v4")
# setwd("C:/BatchProcessing/WDTF_V2A/WDTF_script_v2")

## ------------------------------------------------------------------------
configJson <- fromJSON("config_file.json")
mappingJson <- fromJSON("data_frame_mapping.json")

## ------------------------------------------------------------------------
source("timeseries_client.R")

## ------------------------------------------------------------------------
 timeseries$connect(configJson$config$server$aqServer, configJson$config$server$aqUsername, configJson$config$server$aqPassword)
 timeseries$disconnect()
 timeseries$connect(configJson$config$server$aqServer, configJson$config$server$aqUsername, configJson$config$server$aqPassword)

## ------------------------------------------------------------------------
timeSeriesName = paste0(mappingJson$paramsInfo["HG","aqParamCode"],'.', configJson$siteInfo[["2219-1"]]$tslabels[1],'@',"2219-1")
locationData = timeseries$getLocationData(timeseries$getLocationIdentifier(timeSeriesName))
#print(locationData)

## ------------------------------------------------------------------------
getTimeSeriesDataFrame = function(timeSeriesName){
  
  # print(paste0("getTimeSeriesDataBeg",Sys.time()))
  
  # Get the location data
  locationData = timeseries$getLocationData(timeseries$getLocationIdentifier(timeSeriesName))
  locationName = locationData$LocationName
  latitude = locationData$Latitude
  longitude = locationData$Longitude
  utcOffset = timeseries$getUtcOffsetText(locationData$UtcOffset)
  
  
  # event period since "ONE DAY" (TOURAJ)
  if(configJson$config$timeRangeSpecifications$timeEnd == "") { timeEnd = Sys.time() } else { timeEnd = as.POSIXlt(configJson$config$timeRangeSpecifications$timeEnd)}
  fromPeriodStart = timeEnd - 24*60*60
  toPeriodEnd = timeEnd
  

  
  #Timeseries client proper event period format (RAFA)
  startOfDay = 'T00:00:00'
  endOfDay = 'T23:59:59.9999999'
  if(configJson$config$timeRangeSpecifications$incrementalMode == TRUE){
    
    fromEventPeriodStart = paste0(paste0((Sys.Date() - 1), startOfDay),utcOffset)
    toEventPeriodEnd = paste0(paste0((Sys.Date() - 1), endOfDay),utcOffset)
    
  }else{
    
    fromEventPeriodStart = paste0(configJson$config$timeRangeSpecifications$eventPeriodStartDay, utcOffset)
    toEventPeriodEnd = paste0(configJson$config$timeRangeSpecifications$eventPeriodEndDay, utcOffset)
    
  }
  
  eventPeriodLabel = sprintf('%s - %s', configJson$config$timeRangeSpecifications$eventPeriodStartDay, configJson$config$timeRangeSpecifications$eventPeriodEndDay)
  
  #read time-series from AQ API
  json <- timeseries$getTimeSeriesCorrectedData(timeSeriesName,
                                       queryFrom = fromEventPeriodStart,
                                       queryTo = toEventPeriodEnd)
  
  timeSeriesData <- list("offset" = utcOffset, "dataFrame" = json, "locationName" = locationName, "latitude" = latitude, "longitude" = longitude)
  
  # print(paste0("getTimeSeriesDataEnd",Sys.time()))
  
  return(timeSeriesData)
  
}

## ------------------------------------------------------------------------
getObservationMember = function(siteId, paramCode,fileConn) {
  
  # print(paste0("getObservationMemberBeg",Sys.time()))
  
  # Variables that access config file
  wdtfOwnerId = configJson$siteInfo[[siteId]]$dataOwner
  security = configJson$siteInfo[[siteId]]$securityConstraints
  status = configJson$siteInfo[[siteId]]$status
  
  #Variables that access mapping file
  gmlId = mappingJson$paramsInfo[paramCode,"gmlIds"]
  feature = mappingJson$paramsInfo[paramCode,"features"] 
  interpol = mappingJson$paramsInfo[paramCode,"interpols"]
  units = mappingJson$paramsInfo[paramCode,"units"]
  
  #Default quality
  defaultQuality = "quality-E"
  
  
  #Id used to keep track of different parameters and creating timeseries name
  idx = which(configJson$siteInfo[[siteId]]$params == paramCode)
  timeSeriesName = paste0(mappingJson$paramsInfo[paramCode,"aqParamCode"],'.', configJson$siteInfo[[siteId]]$tslabels[idx],'@',siteId)
  print(paste0('Retrieving time-series for:', timeSeriesName))
  
  #Things that need the idx
  regulationCode = configJson$siteInfo[[siteId]]$regulationProperty[idx]
  datum = mappingJson$datum[configJson$siteInfo[[siteId]]$datum[idx], "urn"]
  
  
  
  
  #Timeseries name version 2
  #timeSeriesNamev2 = configJson$siteInfo[[siteId]]$siteName
  
  
  #Samplinggroup and SamplingPoint parsing
  if(grepl("-", siteId)){
   siteId_split = strsplit(siteId, "-")
   samplingGroup = siteId_split[[1]][[1]]
   samplingPoint = siteId_split[[1]][[2]] 
  } else {
    print("Incorrect siteId fromatting")
  }
  
  timeSeriesData <- getTimeSeriesDataFrame(timeSeriesName)
  
  json <- timeSeriesData$dataFrame
  
  
  #REMOVED
  #<gml:description>Water data</gml:description>
  
  if (json$NumPoints > 0) {
    # convert AQTS timestamps to POSIXct values
    timeStamp2 = strptime(substr(json$Points$Timestamp,0,19), "%FT%T")
    vals = json$Points$Value
    grades = json$Points$Grade
    
    #CHANGE
    timeStamp = strftime( timeStamp2 , paste0("%Y-%m-%dT%H:%M:%S", timeSeriesData$offset))
  
    #observationMember header  
      wdtf_obsMember = paste0(
'    <wdtf:observationMember>
    <wdtf:TimeSeriesObservation gml:id="', gmlId , '">
       <gml:name codeSpace="http://www.bom.gov.au/std/water/xml/wio0.2/feature/TimeSeriesObservation/' , wdtfOwnerId , '/', samplingGroup , '/',samplingPoint,'/' , feature , '/">1</gml:name>
       <om:procedure xlink:href="urn:ogc:def:nil:OGC:unknown"/>
       <om:observedProperty xlink:href="http://www.bom.gov.au/std/water/xml/wio0.2/property//bom/' , feature , '"/>
       <om:featureOfInterest xlink:href="http://www.bom.gov.au/std/water/xml/wio0.2/feature/SamplingPoint/' , wdtfOwnerId , '/' , samplingGroup , '/',samplingPoint,'"/>
       <wdtf:relatedSamplingFeature xlink:href="http://www.bom.gov.au/std/water/xml/wio0.2/feature/SamplingGroup/', wdtfOwnerId ,'/', samplingGroup, '"/>
       <wdtf:metadata>
          <wdtf:TimeSeriesObservationMetadata>
            <wdtf:relatedTransaction xlink:href="#synch',gmlId,'"', '/>
               <wdtf:regulationProperty>',regulationCode,'</wdtf:regulationProperty>
               <wdtf:securityConstraints>',security,'</wdtf:securityConstraints>
               <wdtf:status>',status,'</wdtf:status>'
      )
      
      
      if(paramCode == "HG"|| paramCode == "QR") {
        wdtf_obsMember = paste0(wdtf_obsMember,
        '
               <wdtf:datum>urn:ogc:def:datum:bom::',datum,'</wdtf:datum>'
        )
      } 
               


      wdtf_obsMember = paste0(wdtf_obsMember,
      ' 
          </wdtf:TimeSeriesObservationMetadata>
       </wdtf:metadata>
       <wdtf:result>
          <wdtf:TimeSeries>
          <wdtf:defaultInterpolationType>' , interpol , '</wdtf:defaultInterpolationType>
          <wdtf:defaultUnitsOfMeasure>' , units , '</wdtf:defaultUnitsOfMeasure>
          <wdtf:defaultQuality>',defaultQuality,'</wdtf:defaultQuality>'
      )
      
      #CHANGE
      writeLines(wdtf_obsMember, fileConn)
  
      #MAIN CHANGE
      #now populate observed members for given observed property
      for(iObs in 1:length(timeStamp2)) {
        
        if(!is.na(vals[iObs])) {
          # if(gradeConv == defaultQuality){
          if(iObs != 1){
            
            writeLines(paste0('              <wdtf:timeValuePair time="', timeStamp[iObs], '">', vals[iObs], '</wdtf:timeValuePair>'), fileConn)
          
            } else {
            
            writeLines(paste0('              <wdtf:timeValuePair time="', timeStamp[iObs],'" quality="', defaultQuality ,'">', vals[iObs], '</wdtf:timeValuePair>'), fileConn)
          
              }
        } else {
          
          writeLines(paste0('              <wdtf:timeValuePair time="', timeStamp[iObs],'" xsi:nil="true" quality="quality-F"/>'), fileConn)       
        
          }
        
     }
    
    #Add observationMember tail  
      writeLines(
        '          </wdtf:TimeSeries>
       </wdtf:result>
    </wdtf:TimeSeriesObservation>
  </wdtf:observationMember>', fileConn)
      
    
    # print(paste0("getObservationMemberEnd",Sys.time()))
    
    return(wdtf_obsMember)
  
  } else {
    return("")      
  }
}

## ------------------------------------------------------------------------
get_hydrocollection = function(siteId, wdtfFileName) {
  
   # print(paste0("get_hydrocollectionBeg",Sys.time()))
   
   #Timeseries name version 2
   #timeSeriesNamev2 = configJson$siteInfo[[siteId]]$siteName
   #print(timeSeriesNamev2)

   t = Sys.time()   
   wdtfProviderId = configJson$siteInfo[[siteId]]$dataProvider
   wdtfOwnerId = configJson$siteInfo[[siteId]]$dataOwner
   tstr = strftime(t, "%Y%m%d%H%M%S")
   gmlId = paste0(wdtfOwnerId,'-', tstr, '-ctsd')
   
   #CHANGE
   filePath = file.path(configJson$config$ftpPath$localTemPath, wdtfFileName)
   fileConn<-file(filePath,'w')
 
  
  wdtfCollection = paste0(
'<?xml version="1.0"?>
<wdtf:HydroCollection
  xmlns:sa="http://www.opengis.net/sampling/1.0/sf1"
  xmlns:om="http://www.opengis.net/om/1.0/sf1"
  xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
  xmlns:xlink="http://www.w3.org/1999/xlink"
  xmlns:gml="http://www.opengis.net/gml"
  xmlns:wdtf="http://www.bom.gov.au/std/water/xml/wdtf/1.0"
  xmlns:ahgf="http://www.bom.gov.au/std/water/xml/ahgf/0.2"
  xsi:schemaLocation="http://www.opengis.net/sampling/1.0/sf1 ../sampling/sampling.xsd 
  http://www.bom.gov.au/std/water/xml/wdtf/1.0 ../wdtf/water.xsd
  http://www.bom.gov.au/std/water/xml/ahgf/0.2 ../ahgf/waterFeatures.xsd"
  gml:id="', gmlId, '">
  <gml:description>Data Exported from AQUARIUS</gml:description>
  <gml:name codeSpace="http://www.bom.gov.au/std/water/xml/wio0.2/feature/HydroCollection/' , wdtfOwnerId , '/">' ,wdtfFileName, '</gml:name>
  <wdtf:metadata>
    <wdtf:DocumentInfo>
      <wdtf:version>wdtf-package-v1.0.2</wdtf:version>
      <wdtf:dataOwner codeSpace="http://www.bom.gov.au/std/water/xml/wio0.2/party/person/bom/">',  wdtfOwnerId , '</wdtf:dataOwner>
      <wdtf:dataProvider codeSpace="http://www.bom.gov.au/std/water/xml/wio0.2/party/person/bom/">' , wdtfProviderId, '</wdtf:dataProvider>
      <wdtf:generationDate>' , strftime(t,paste0("%Y-%m-%dT%H:%M:%S", configJson$config$timeRangeSpecifications$utcOffset)) , '</wdtf:generationDate>
      <wdtf:generationSystem>AQUARIUS</wdtf:generationSystem>
    </wdtf:DocumentInfo>
  </wdtf:metadata>'
)	  
  
  for(param in configJson$siteInfo[[siteId]]$params){
    
   gmlId = mappingJson$paramsInfo[param,"gmlIds"]  
    
   #Duplicate code (refactor)
   idx = which(configJson$siteInfo[[siteId]]$params == param)
   timeSeriesName = paste0(mappingJson$paramsInfo[param,"aqParamCode"],'.', configJson$siteInfo[[siteId]]$tslabels[idx],'@',siteId)
   print(paste0('Retrieving beginning and end timestamps for:', timeSeriesName))
  
   timeSeriesData <- getTimeSeriesDataFrame(timeSeriesName)
  
   df <- timeSeriesData$dataFrame
   
   if (df$NumPoints > 0) {
   # convert AQTS timestamps to POSIXct values
   timeStamp3 = strptime(substr(df$Points$Timestamp,0,19), "%FT%T")
   vals = df$Points$Value}
   
   #Timestamps for synchronazation of each time series at the top of the xml file
   timeStamp4 = strftime( timeStamp3[1] , paste0("%Y-%m-%dT%H:%M:%S", timeSeriesData$offset))
   timeStamp5 = strftime( timeStamp3[length(timeStamp3)] , paste0("%Y-%m-%dT%H:%M:%S", timeSeriesData$offset))
    
    wdtfCollection = paste0(wdtfCollection,
      '<wdtf:transactionMember>		
      <wdtf:SynchronizationTransaction gml:id="synch',gmlId,'">		
         <wdtf:period>		
            <om:TimePeriod>		
               <om:begin>', timeStamp4 ,'</om:begin>		
               <om:end>', timeStamp5 ,'</om:end>		
            </om:TimePeriod>		
         </wdtf:period>		
      </wdtf:SynchronizationTransaction>		
    </wdtf:transactionMember>'
    )  
  }
  
    #CHANGE
    writeLines(wdtfCollection, fileConn)
  
  
    obsMemberCount = 0
    paramLen = length(configJson$siteInfo[[siteId]]$params)
    for(param in configJson$siteInfo[[siteId]]$params) {   
      tryCatch(
      {
        obsMember = getObservationMember(siteId, param, fileConn)
        if(obsMember != "") { obsMemberCount = obsMemberCount+1} 
        wdtfCollection = paste0(wdtfCollection, obsMember)
      },
      error = function(cond) {
        print(cond)
        #just continue loop and do nothing for that parameter
      })
    }
 
    
    #CHANGE
    writeLines('</wdtf:HydroCollection>', fileConn)
    
    #CHANGE
    close(fileConn) 
  
  if(obsMemberCount == 0) {  
       return(NA)
  }
    
    # print(paste0("get_hydrocollectionEnd",Sys.time()))
    
  return(wdtfCollection)    
}

## ------------------------------------------------------------------------
getMetadata = function(siteId,wdtfFileName,samplingGroup,samplingPoint) {
  
  
  # print(paste0("getMetadataBeg",Sys.time()))
  
  timeSeriesName = paste0(mappingJson$paramsInfo[configJson$siteInfo[[siteId]]$params[[1]],"aqParamCode"],'.', configJson$siteInfo[[siteId]]$tslabels[1],'@',siteId)
  timeSeriesData <- getTimeSeriesDataFrame(timeSeriesName)
  locationName <- timeSeriesData$locationName
  latitude <- timeSeriesData$latitude
  longitude <- timeSeriesData$longitude
  offset <- timeSeriesData$offset
  
  wdtfOwnerId = configJson$siteInfo[[siteId]]$dataOwner
  t = Sys.time()
   
  metadata = paste0(
'<?xml version="1.0" ?>
<wdtf:HydroCollection xmlns:wdtf="http://www.bom.gov.au/std/water/xml/wdtf/1.0" 
xmlns:ahgf="http://www.bom.gov.au/std/water/xml/ahgf/0.2" 
xmlns:gml="http://www.opengis.net/gml" 
xmlns:om="http://www.opengis.net/om/1.0/sf1" 
xmlns:sa="http://www.opengis.net/sampling/1.0/sf1" 
xmlns:xlink="http://www.w3.org/1999/xlink" 
xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" 
xsi:schemaLocation="http://www.opengis.net/sampling/1.0/sf1 ../sampling/sampling.xsd http://www.bom.gov.au/std/water/xml/wdtf/1.0 ../wdtf/water.xsd http://www.bom.gov.au/std/water/xml/ahgf/0.2 ../ahgf/waterFeatures.xsd" gml:id="',wdtfOwnerId,'-',strftime(t, "%Y%m%d%H%M%S"),'">
   <gml:description>Data Exported from AQUARIUS</gml:description>
   <gml:name codeSpace="http://www.bom.gov.au/std/water/xml/wio0.2/feature/HydroCollection/' , wdtfOwnerId , '/">' ,wdtfFileName, '</gml:name>
   <wdtf:metadata>
      <wdtf:DocumentInfo>
         <wdtf:version>wdtf-package-v1.0.2</wdtf:version>
         <wdtf:dataOwner codeSpace="http://www.bom.gov.au/std/water/xml/wio0.2/party/person/bom/">',wdtfOwnerId,'</wdtf:dataOwner>
         <wdtf:dataProvider codeSpace="http://www.bom.gov.au/std/water/xml/wio0.2/party/person/bom/">',wdtfOwnerId,'</wdtf:dataProvider>
         <wdtf:generationDate>' , strftime(t,paste0("%Y-%m-%dT%H:%M:%S", configJson$config$timeRangeSpecifications$utcOffset)) , '</wdtf:generationDate>
         <wdtf:generationSystem>AQUARIUS</wdtf:generationSystem>
      </wdtf:DocumentInfo>
   </wdtf:metadata>
   <!-- Site and Location Details -->
   <wdtf:siteMember>
      <wdtf:SamplingGroup gml:id="s',  samplingGroup ,'">
         <gml:name codeSpace="http://www.bom.gov.au/std/water/xml/wio0.2/feature/SamplingGroup/',wdtfOwnerId,'/">',samplingGroup,'</gml:name>
         <sa:sampledFeature xlink:href="urn:ogc:def:nil:OGC:unknown" />
         <wdtf:shortName>',locationName,'(',samplingGroup,')</wdtf:shortName>
         <wdtf:longName>',locationName,'</wdtf:longName>
         <wdtf:location>
            <gml:Point srsName="urn:ogc:def:crs:EPSG::4283">
               <gml:pos>',latitude,' ',longitude,'</gml:pos>
            </gml:Point>
         </wdtf:location>
         <wdtf:timeZone>',offset,'</wdtf:timeZone>
      </wdtf:SamplingGroup>
   </wdtf:siteMember>
   <wdtf:siteMember>
      <wdtf:SamplingPoint gml:id="l',samplingPoint,'">
         <gml:name codeSpace="http://www.bom.gov.au/std/water/xml/wio0.2/feature/SamplingPoint/w00072/">',samplingGroup,'/',samplingPoint,'</gml:name>
         <sa:sampledFeature xlink:href="urn:ogc:def:nil:OGC:unknown" />
         <sa:relatedSamplingFeature xlink:href="#s',samplingGroup,'" xlink:arcrole="http://www.bom.gov.au/std/water/xml/wio0.2/definition/arcrole/bom/parent" />
         <sa:surveyDetails>
            <sa:SurveyProcedure gml:id="l1method">
               <sa:positionMethod xlink:href="http://www.bom.gov.au/std/water/xml/wio0.2/procedure/positionMethod/bom/approximate" />
            </sa:SurveyProcedure>
         </sa:surveyDetails>
         <sa:position>
            <gml:Point srsName="urn:ogc:def:crs:EPSG::4283">
               <gml:pos>',latitude,' ',longitude,'</gml:pos>
            </gml:Point>
         </sa:position>
         <wdtf:shortName>',locationName,'</wdtf:shortName>
         <wdtf:longName>',locationName,'</wdtf:longName>
         <wdtf:timeZone>',offset,'</wdtf:timeZone>
      </wdtf:SamplingPoint>
   </wdtf:siteMember>
</wdtf:HydroCollection>
  ')
  
  # print(paste0("getMetadataEnd",Sys.time()))
  
  return(metadata)
  
}

## ------------------------------------------------------------------------
makeWdtfZip = function() {
  
  #Getting sampleing point (probably should refactor and verify - also must change Stage to HG)
  
  
  # print(paste0("makeZipBeg",Sys.time()))
  
  
  t = Sys.time() 
  wdtfFileNames = vector(mode = 'character', length=0)
  zipFileNames = vector(mode = 'character', length=0)
  for(iSite in 1:length(configJson$siteInfo)) {
    
    siteId = configJson$siteInfo[[iSite]]$siteID
    if(grepl("-",siteId)){
      siteId_split = strsplit(siteId, "-")
      samplingGroup = siteId_split[[1]][[1]] 
      samplingPoint = siteId_split[[1]][[2]]
    } else {
      print("Incorrect siteId format") 
    }
    wdtfOwnerId = configJson$siteInfo[[siteId]]$dataOwner
    wdtfFileName = paste0("wdtf.",wdtfOwnerId, '.' , strftime(t, "%Y%m%d%H%M%S") ,'.', samplingGroup , "-ctsd.xml")
    wdtfCollection = get_hydrocollection(siteId, wdtfFileName)
    if(!is.na(wdtfCollection)) {
        wdtfFileNames = c(wdtfFileNames, wdtfFileName)
        filePath = file.path(configJson$config$ftpPath$localTemPath, wdtfFileName)
        #if (file.exists(filePath)) file.remove(filePath)
        print(paste0("Writing ", filePath," ..."))
        #CHANGE
        #write(wdtfCollection, file = filePath)
    }
    
    wdtfFileName = paste0("wdtf.",wdtfOwnerId, '.' , strftime(t, "%Y%m%d%H%M%S") ,'.', samplingGroup , ".xml")
    wdtfCollection = getMetadata(siteId,wdtfFileName,samplingGroup,samplingPoint)
    if(!is.na(wdtfCollection)) {
      wdtfFileNames = c(wdtfFileNames, wdtfFileName)
      filePath = file.path(configJson$config$ftpPath$localTemPath, wdtfFileName)
      #if (file.exists(filePath)) file.remove(filePath)
      print(paste0("Writing ", filePath," ..."))
      write(wdtfCollection, file = filePath)
    }
    
    wdtfOwnerId = "DPIPWE"
    cwd = getwd()
    tryCatch({
      setwd(configJson$config$ftpPath$localTemPath)
      zipFileName = paste0(samplingGroup,'.',wdtfOwnerId, '.' , strftime(t, "%Y%m%d%H%M%S") , ".zip" )
      #if (file.exists(zipFileName)) file.remove(zipFileName)
      zip(zipFileName, wdtfFileNames)
    }, finally = {
      setwd(cwd)
    })
    zipFileNames = c(zipFileNames,zipFileName)
    wdtfFileNames = vector(mode = 'character', length=0)
    
  }
  
  # print(paste0("makeZipEnd",Sys.time()))
    
  return(zipFileNames)
  
 
}

## ------------------------------------------------------------------------
UploadWdtf2Ftp = function(zipFileNames){
  
  # print(paste0("uploadZipBeg",Sys.time()))
  
  server <- configJson$config$ftpServer$ftpTestServer
  credentials <- paste0(configJson$config$ftpServer$ftpTestUsername,':',configJson$config$ftpServer$ftpTestPassword)
  
  if(configJson$config$ftpPath$useFtp == TRUE) {
    server <- configJson$config$ftpServer$ftpServer
    credentials <- paste0(configJson$config$ftpServer$ftpUsername,':',configJson$config$ftpServer$ftpPassword)
  }
  for(z in zipFileNames) {
    localPath <- file.path(configJson$config$ftpPath$localTemPath, z)
    #REVERT
    uploadPath <- file.path(server, "WDTF", z)
    # uploadPath <- file.path(server, "incoming/data", z)
    print(uploadPath)
    
    ftpUpload(localPath, uploadPath, userpwd = credentials)  
  }
  
  # print(paste0("uploadZipEnd",Sys.time()))
  
}


## ------------------------------------------------------------------------
zipFileNames = makeWdtfZip()
#REVERT

#UploadWdtf2Ftp(zipFileNames)

print(paste0(zipFileNames," file Successfully created and uploaded into FTP site"))

