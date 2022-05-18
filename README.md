# BabyVW

For best results, BabyVW currently uses Unity 2020.2.1.

This repository depends on VoxSim: https://www.github.com/VoxML/VoxSim.  Clone BabyVW, then:

Download the required VoxSim assets as a package [here](https://github.com/VoxML/voxicon/blob/master/packages/VoxSimPlatform.unitypackage.zip?raw=true), and extract the Unity package from the zip file. In Unity, delete the file that is in the Plugins folder titled `VoxSimPlatform`. Import the downloaded Unity package. Everything should appear in the *Plugins* folder.

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
(ml-agents) $ pip install sklearn
```

## Additional Dependencies (optional)

The BabyVW learning environment uses Stable-Baselines3, which is written using PyTorch, but some minor functionality, such as converting SB3 TensorBoard plots to PyPlot plots, requires TensorFlow. To use this functionality on:

### Silicon Macs with the M1 chip:
```
(ml-agents) $ conda install -c apple tensorflow-deps
(ml-agents) $ pip install tensorflow-macos
(ml-agents) $ pip install tensorflow-metal
```

### All other systems:
```
(ml-agents) $ pip install tensorflow
```

The main training and testing pipeline should work fine without these dependencies.

**Next, check out an environment and agent, e.g., the [stacker](https://github.com/VoxML/BabyVW/blob/master/Stacker-Agent.md)**
