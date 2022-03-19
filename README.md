# BabyVW

For best results, BabyVW currently uses Unity 2020.2.1.

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

# Testing a model

First, make sure both elements under "Interactable Object Types" are set to "Cube" (as below).

<img width="400" alt="image" src="https://user-images.githubusercontent.com/11696878/153765818-90f8eafe-1574-4a25-86d4-09c3cfdfa63e.png">

To change the object types, drag any of the child objects of `ObjectPrefabs` from the hierarchy onto "Interactable Object Types" to repopulate the fields.

Make sure `VectorDDPGAgent` (under `AgentArchitectures`) is enabled and all others are disabled (only one agent architecture should be enabled at a time):

<img width="250" alt="image" src="https://user-images.githubusercontent.com/11696878/153765917-95f564dc-9d02-4b79-a751-8e4a21bbd084.png">

Make sure "Continuous Stcking Agent" (component of `VectorDDPGAgent`) is set as below:

<img width="400" alt="image" src="https://user-images.githubusercontent.com/11696878/159133389-a90ffb58-d629-423e-95c8-a5adda047844.png">

Then, run the following command from within the `ml-agents` Conda environment: `python ddpg.py -b stacker -l cube_stacking_model -t 100 -m 2cubes-20211212-0.0,0.0-1000.0,1000.0-2000-COG.HGT --vector_obs --test -p COG HGT`, and play the Unity scene.  This should connect and test one of the provided pretrained policies for 100 timesteps, print a summary, and generate a reward plot!

(e.g.,)
```
===== Summary =====
Tested for 100 timesteps
	69 episodes
	Total reward: 66342.0
	Max reward achieved: 1000.0
	Mean reward per episode: 961.4783
```

<img width="640" alt="image" src="https://user-images.githubusercontent.com/11696878/153766179-5db11c46-7edd-4d5a-a922-751b5ab9797a.png">

# Training a model

Using the same Unity settings as above, run `python ddpg.py -b stacker -l cube_stacking_model -t 2000 -m <your model name here> --vector_obs --train -p COG HGT`.  This will train a model using the provided DDPG policy in the `cube_stacking_model` directory for 2000 timesteps (about 30 minutes on a Mac M1).  The saved model will have a lot of automatically-generated suffixes attached, such as `0.0,0.0-1000.0,1000.0`, which encode certain parameters of the action space, observation space, and training regime, to help identify the properties of each saved model after the fact.

# Fine-tuning a model

Fine tuning uses the same procedure as above, except the command is changed slightly:
`python ddpg.py -b stacker -l cube_stacking_model -t 2000 -m <oldModel> -M <newModel> --vector_obs --train -p COG HGT`

Instead of initializing weights randomly, this command loads up the weights of `oldModel`, continues training for `t` timesteps, and saves the result at `newModel`.
