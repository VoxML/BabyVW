# BabyVW

For best results, BabyVW currently uses Unity 2020.2.1.

This repository depends on VoxSim: https://www.github.com/VoxML/VoxSim.  Clone BabyVW, then:

```
$ git submodule init
$ git submodule update --remote --merge
```

Finally, follow the depedency installation instructions at the VoxSim link.

## Setting Up Learning Environment

The learning environment is based on Unity ML-Agents. The Unity ML-Agents package is included in `manifest.json` and should be available when this repository is cloned.  To complete the setup of the learning environment, you should:

```
$ conda create --name ml-agents python=3.8
$ conda activate ml-agents
(ml-agents) $ conda install grpcio
(ml-agents) $ pip install numpy
(ml-agents) $ pip install stable_baselines3
(ml-agents) $ python -m pip install mlagents==0.28.0
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

## Entire Environments

You can create the entire environment (including optional dependencies) in one command by running:

* (On M1 Macs) `conda create --name ml-agents --file ml-agents-conda-mac-m1.txt`.
* (On Intel Macs) `conda create --name ml-agents --file ml-agents-conda-mac-intel.txt`.
* (On Windows) `conda create --name ml-agents --file ml-agents-conda-win.txt`.

**Next, check out an environment and agent, e.g., the [stacker](https://github.com/VoxML/BabyVW/blob/master/Stacker-Agent.md)**
