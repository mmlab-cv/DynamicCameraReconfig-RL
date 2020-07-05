# HeterogenousCameraNetwork
Heterogenous camera network, with fixed PTZ and UAV-mounted cameras

The simulator structure can be divided in two main layers:
1. Crowd behavior simulation
2. Camera network deploying and control

## Crowd behavior simulation
The movement of pedestrian in the environments is based on the [Social Force model](https://arxiv.org/pdf/cond-mat/9805244).
The visula appearance of the crowd is generated using the [UMA library](https://github.com/umasteeringgroup/UMA)

## Camera network deploying and control

## Requirements

1. [Unity 2019.1.0f2](unityhub://2019.1.0f2/292b93d75a2c)





### How to run RL-simulations

1. Open scene `UcyZara`
2. Choose alpha value in the `GridController`  component inside the `Map` GameObject
3. Select the `Pseudo Academy` Object and assign the inference model to one of the available models in `Assets/Scripts/agents-scripts/Models from the paper`
4. Press play