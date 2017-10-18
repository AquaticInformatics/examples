# idf-analysis.r
#
# code to conduct idf analysis for annual extreme rainfall data
# generates two graphs:
#  - graphs of i vs T for each duration on a multi-panel plot
#  - plot of i vs. D for a range of return periods
#
# 2017-July-19 Touraj
##############################################################################################

source("Gumplot-idf.R")

IdfAnalysis = function(depthMatrix, overlayIntensities, durations, Tp, isPDF, plotTitle, eventPeriodLabel, unitId) {
  
  nd = length(durations)   
  KTp = -(sqrt(6)/pi)*(0.5772 + log(log(Tp/(Tp-1))))          # frequency factors for method of moments fit
  nTp = length(Tp)
  depths = depthMatrix[ , 2:(nd+1)] 
  
  
  ####################################################################################
  #  Set up multi-panel plot to show distributions for each duration
  ####################################################################################
 
  windows()  

  if(isPDF)
    pdf("distribution-plot.pdf",width=10,height=7)

  # set parameters for a multi-panel plot with 5 rows and 2 columns 
  par(mfrow = c(5,2), mar = c(1,16,1,1)*0.25, oma = c(5, 5,1,1), cex = 1)
  # generate blank plot in top left panel and then add legend 




  plot(durations, type = "n", bty = "n", xaxt = "n", yaxt = "n", xlab = "", ylab = "")  
  legend("left", bty = "n", pch = c(21, NA, NA), pt.bg = c("red", NA, NA), lty = c(0, 1, 3), col = c("black", "black", "black"), 
         legend = c("observed", "fit", "95% conf. limit"), title = "IDF Distributions", title.adj = 0
  )

  # set character size for remaining graph panels 
  par(cex = 0.75) 


  ####################################################################################
  #  Loop through durations and conduct analysis; add new plot of distributions to
  #  the multi-panel plot in each pass through the loop
  ####################################################################################

  idf = matrix(nrow = nd, ncol = nTp)
  for (id in 1:nd) {
    di = depths[, id]                               # extract id-th column from matrix
    ni = length(di)                                 # length of series
    int = di/durations[id]                          # convert depths to intensities in mm/hr
    ri = ni + 1 - rank(int)                         # compute rank of each intensity, with ri = 1 for the largest
    Ti = (ni + 1)/ri                                # calculate plotting position
    Gumplotidf(durations[id], int, Ti, id, unitId)  # plot intensity vs. T on Gumbel axes
    idf[id, ] = mean(int) + KTp*sd(int)             # intensities at different return periods for current duration
  }


  ####################################################################################
  #  Generate idf plot
  ####################################################################################

  # set up to use different plotting symbols for each return period 
  psym = seq(1, nTp)
  color = c("black","blue","brown","dark green","orange","magenta","dark cyan") 

  if(isPDF)
    dev.off()


  # open new window for this graph, set plotting parameters for a single graph panel 
  windows()          
  par(mfrow = c(1,1), mar = c(5, 5, 5, 5), cex = 1)

  # set up custom axis labels and grid line locations
  ytick = c(1,2,3,4,5,6,7,8,9,10,20,30,40,50,60,70,80,90,100,
            200,300,400,500,600,700,800,900,1000)
  yticklab = as.character(ytick)

  xgrid = c(5,6,7,8,9,10,15,20,30,40,50,60,120,180,240,300,360,
            420,480,540,600,660,720,840,960,1080,1200,1320,1440)

  xtick = c(5,10,15,20,30,60,120,360,720,1440)
  xticklab = c("5","10","15","20","30","60","2","6","12","24")


  ymax = max(idf)
  durations = durations*60    # change durations to minutes

  if(isPDF)
     pdf("idf-plot.pdf",width=7,height=5)

  # plot i vs D for first return period  
  plot(durations, idf[, 1], 
       xaxt="n",yaxt="n",
       pch = psym[1], log = "xy", col = "white",
       xlim = c(4, 24*60), ylim = c(1, 100),
     
       #for testing diffrent limits
       #xlim = c(4, 24*60), ylim = c(.01, 100),
       #xlim = c(12, 24*60), ylim = c(.004, 50),
       #xlim = c(30, 24*60), ylim = c(0.01, 100),
       xlab = "(min)          Duration          (hr)",
       ylab = sprintf("Intensity (%s/hr)", unitId),
       main= c("Intensity-Duration-Frequency (IDF) Plot for ",plotTitle)
  )

  # plot i vs D for remaining return periods, use different plotting symbols for each T
  #for (iT in 2:nTp) {
  #  points(durations, idf[, iT], pch = psym[iT], col = color[iT])
  #}

  points(durations, overlayIntensities/(durations/60), pch = 21, bg = "red", col = "red")

  # add best-fit regression lines for each return period  
  for (iT in 1:nTp) {
    mod.lm = lm(log10(idf[, iT]) ~ log10(durations))
    b0 = mod.lm$coef[1]
    b1 = mod.lm$coef[2]
    yfit = 10^(b0 + b1*log10(durations))
    lines(durations, yfit, lty = psym[iT], col = color[iT], lwd=2)
  }

  # add custom axes  
  axis(1, xtick, xticklab)
  axis(2, ytick, yticklab)

  # add grid lines  
  abline(v = xgrid, col = "gray")
  abline(h = ytick, col = "gray")

  # add legend to lower left corner  
  par(cex = 0.75)
    legend("topright", pt.bg = "red", bg = "white", lwd = 2, pch = c(psym * NaN, 21, NaN), lty = c(psym, NaN, NaN), col = c(color, "red", NaN), legend = c(as.character(Tp), "Observed event(s) during:", eventPeriodLabel), title = "Return period (yr)", title.adj = 0.5)

  if(isPDF)
     dev.off()

}