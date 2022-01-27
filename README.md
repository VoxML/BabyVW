# BabyVW

This repository depends on VoxSim: https://www.github.com/VoxML/VoxSim.  Clone BabyVW, then:

```
$ git submodule init
$ git submodule update --remote --merge
```

Finally, follow the depedency installation instructions at the VoxSim link.

## Setting Up Learning Environment

The learning environment integrates Unity ML-Agents, OpenAI Gym, and the [Stable-Baselines3](https://stable-baselines3.readthedocs.io/en/master/) reinforcement learning package.  The Unity ML-Agents package is included in `manifest.json` and should be available when this repository is cloned.  To complete the setup of the learning environment, you should:

```
$ conda create --name ml-agents python=3.8
$ conda activate ml-agents
(ml-agents) $ conda install grpcio
(ml-agents) $ pip install numpy
(ml-agents) $ pip install stable_baselines3
(ml-agents) $ pip install mlagents_envs
(ml-agents) $ pip install tensorboard
(ml-agents) $ pip install notebook
```

On new Silicon Macs with the M1 chip, you can create the environment in one command by running `conda create --name ml-agents --file ml-agents-conda-mac-m1.txt`.
