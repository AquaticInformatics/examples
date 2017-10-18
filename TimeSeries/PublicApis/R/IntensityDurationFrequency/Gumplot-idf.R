# Gumplotidf.r
#
# function to generate Gumbel plot for idf analysis
#
# Thanks to Prof. Saeid Mousavi of SBU in Iran, who provided the code for
# computing the method of moments fit and the 95% confidence limits
#
# 2013-Sept-9 RDM
###############################################################

Gumplotidf = function(duration, int, Ti, id, unitId) {

  ### Define parameters for axis plotting ###

  Ttick = c(1.001, 2,  3,  4, 5,  6,  7,  8,  9, 10, 20, 30, 40, 50, 60, 70, 80, 90, 100, 200)
  xtlab = c(1.001, 2, NA, NA, 5, NA, NA, NA, NA, 10, NA, NA, NA, 50, NA, NA, NA, NA, 100, 200)

  y = -log(-log(1 - 1/Ti))
  ytick = -log(-log(1 - 1/Ttick))
  xmin = min(min(y), min(ytick))
  xmax = max(ytick)

  ### Fit distribution by method of moments ###

  KTtick = -(sqrt(6)/pi)*(0.5772 + log(log(Ttick/(Ttick-1))))
  intTtick = mean(int) + KTtick*sd(int)
  nint = length(int)
  se = (sd(int)*sqrt((1+1.14*KTtick + 1.1*KTtick^2)))/sqrt(nint)
  LB = intTtick - qt(0.975, nint-1)*se
  UB = intTtick + qt(0.975, nint-1)*se
  ymax = max(UB)

  plot( y, int,
        xlab = "", ylab = "",
        xaxt = "n", # yaxt = "n",
        xlim = c(xmin,xmax), ylim = c(0, ymax),
        pch = 21, bg = "red",
        main = ""
  )

  if (id > 7) {
    # add x axes
    axis(side = 1, at = ytick, labels = xtlab)
    mtext(side = 1, text = "T (yr)", line = 3)
  }
  if (id == 4) {
    # add y axis label
    mtext(side = 2, line = 3, text = sprintf("Intensity (%s/hr)", unitId))
  }

  if (duration < 1) {
    # Duration in minutes
    legend("topleft", bty = "n", legend = paste("", duration*60, "min"))
  } else {
    # Duration in hours
    legend("topleft", bty = "n", legend = paste("", duration, "hr"))
  }

  ### Add curve fitted by method of moments, along with 95% confidence intervals ###

  lines(ytick, intTtick, col = "black")
  lines(ytick, LB, col = "black", lty = 3)
  lines(ytick, UB, col = "black", lty = 3)

  return

}
