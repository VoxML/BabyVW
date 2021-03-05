# BabyVW

This repository depends on VoxSim: https://www.github.com/VoxML/VoxSim.  Clone BabyVW, then:

```
$ git submodule init
$ git submodule update --remote --merge
```

Finally, follow the depedency installation instructions at the VoxSim link.

## Installing Unity ML-Agents

Some BabyVW functionality assumes Unity ML-Agents is installed.  The Unity ML-Agents package is included in `manifest.json` and should be available when this repository is cloned.  To complete the installation of the `mlagents` Python package, you should:
1. clone this repo, and follow the setup above (including VoxSim setup), then:
2. `$ python -m venv ./ml-agents`
3. `$ source ml-agents/bin/activate`
4. `(ml-agents) pip3 install numpy==1.18.0`
5. `(ml-agents) pip3 install mlagents==0.24.0`
6. `(ml-agents) pip3 install gym_unity==0.24.0`
7. `(ml-agents) pip3 install Keras`
8. `(ml-agents) pip3 install tensorflow-estimator==2.3.0 tensorflow==2.3.0`
9. `(ml-agents) pip3 install keras-rl2`
