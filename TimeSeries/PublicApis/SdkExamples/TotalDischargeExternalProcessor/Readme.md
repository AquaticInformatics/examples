# TotalDischargeExternalProcessor

This console utility can be scheduled to calculate Total Discharge (QV) for the duration of an event.

Load input signals with 4 configurable values:

- a Count.Bottle time-series identifier from the sampler (any parameter type, no restrictions)
- a QR (discharge) time-series identifier
- a reflected time-series identifier to receive the processor output (TotalDischarge During Event) (QV)
- A minimum sampling duration (default to 2 hours)

Time-series identifiers can be either unique IDs or text labels.

Parameter types of each signal will be validated.

Derive an event period from the instant the bottle count resets to 1, and lasts until 2-hours after the capacity is reached (24-bottle samplers @ 2 hours, or 14-bottle samplers @ 3 hours)

Compute the total discharge volume during the event (use the trapezoidal calc method: the half-way point between each discharge point)

Place the computed Total Discharge value at the start of the event.
