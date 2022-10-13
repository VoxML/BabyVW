# Testing a model

First, make sure both elements under "Interactable Object Types" are set to "Cube" (as below).

<img width="400" alt="image" src="https://user-images.githubusercontent.com/11696878/153765818-90f8eafe-1574-4a25-86d4-09c3cfdfa63e.png">

To change the object types, activate ObjectPrefabs in the inspector and drag any of the child objects of `ObjectPrefabs` from the hierarchy onto "Interactable Object Types" to repopulate the fields.

Make sure `VectorDDPGAgent` (under `AgentArchitectures`) is enabled and all others including Stochastic are disabled (only one agent architecture should be enabled at a time):

<img width="250" alt="image" src="https://user-images.githubusercontent.com/11696878/153765917-95f564dc-9d02-4b79-a751-8e4a21bbd084.png">

Make sure "Continuous Stcking Agent" (component of `VectorDDPGAgent`) is set as below:

<img width="400" alt="image" src="https://user-images.githubusercontent.com/11696878/159133389-a90ffb58-d629-423e-95c8-a5adda047844.png">

Then, run the following command from within the `ml-agents` Conda environment: `python ddpg.py -b stacker -l cube_stacking_model -t 100 -m 2cubes-20211212-0.0,0.0-1000.0,1000.0-2000-COG.HGT --vector_obs --test -p COG HGT`, and play the Unity scene.  This should connect and test one of the provided trained policies for 100 timesteps, print a summary, and generate a reward plot!

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

**Explanation of settable component parameters**
* *Observation Size*: TODO
* *Use Vector Observationse*: TODO
* *Noisy Vectors*: TODO
* *Use Height*: TODO
* *Use Relations*: TODO
* *Use Center of Gravity*: TODO
* *Episode Max Attempts*: Maximum number of time to attempt stacking before giving up.
* *Use All Attempts*: If unchecked, the episode will terminate once the theme object is stacked successfully; otherwise, the agent will make as many attempts as set in `Episode Max Attempts` and not terminate the episode even when successful (the episode only terminates when the max number of attempts is reached, regardless of the result of any individual attempt).
* *Dest Selection Method*: `Highest` if the highest object in the scene should be used as the destination object for the next action, `Consistent` if the destination object should be kept constant across all actions (not suitable for more that 2-object scenarios).
* *Pos Reward Multiplier*: TODO
* *Neg Reward Multiplier*: TODO
* *Observation Space Scale*: TODO
* *Force Multplier*: TODO
* *Save Images*: TODO
* *Write Out Samples*: TODO
* *Out File Name*: TODO
* *Action Space Low*: TODO
* *Action Space High*: TODO
* *Target Action*: TODO
* *Randomize Target Action*: TODO
* *Reward Boost*: TODO

# Training a model

Using the same Unity settings as above, run `python ddpg.py -b stacker -l cube_stacking_model -t 2000 -m <your model name here> --vector_obs --train -p COG HGT`.  This will train a model using the provided DDPG policy in the `cube_stacking_model` directory for 2000 timesteps (about 30 minutes on a Mac M1).  The saved model will have a lot of automatically-generated suffixes attached, such as `0.0,0.0-1000.0,1000.0`, which encode certain parameters of the action space, observation space, and training regime, to help identify the properties of each saved model after the fact.

# Continual Learning

This uses the same procedure as above, except the command is changed slightly:
`python ddpg.py -b stacker -l cube_stacking_model -t 2000 -m <oldModel> -M <newModel> --vector_obs --train -p COG HGT`

Timesteps can have any value.

# Stochastic baseline

The `StochasticAgent` (under `AgentArchitectures`) shares the same flow of control as the other agents, but has no reinforcement learning client attached.  You can use this to place objects randomly on top of the destination object.  As usual, if you use this "agent" all other agents need to be disabled.  Simply set your parameters (e.g., below) and play the scene.

<img width="400" alt="image" src="https://user-images.githubusercontent.com/11696878/159133678-8baa0fff-b34f-4a51-a28b-34b15996637d.png">

**Explanation of settable component parameters**
* (See explanation above)
* *Max Epsiodes*: Maximum number of episodes to execute.
