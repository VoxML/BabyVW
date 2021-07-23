import numpy as np
import gym
from gym import spaces

from mlagents_envs.environment import UnityEnvironment
from mlagents_envs.base_env import ActionTuple

class StackerEnv(gym.Env):
    """
    Description:
        A number of objects are placed in an otherwise empty environment. All
        objects start on the floor and the goal is to place all objects into
        a single stack by placing each in turn  on top of the current stack so
        that the stack stays upright after the placement is complete.
        
    Observation:
        Type: Discrete (4 scalar values)
        The size of this space should be adjusted to always be greater than the
        total number of potentially-stackable objects in the environment. The
        number represents the height of the highest object tower constructed so
        far, i.e., 1 is the default observation because all objects are alone on
        the ground.
        Num     Observation
        0       0 objects have been successfully stacked (would only occur if the environment were empty)
        1       1 object has been successfully stacked (default state because all objects are on the ground)
        2       2 objects have been successfully stacked (highest structure in the environment is a 2-block stack)
        3       3 objects have been successfully stacked
        
    Actions:
        Type: Box (2 dimensions)
        The action space represents where, as a ratio in two dimensions, the theme
        object should be placed relative to the top surface of the destination
        object. That is, an action of [0.0,0.0] would place the theme object at
        the exact center of the top of the destination object, while an action of
        [-1.0,0.0] would place it at the far left edge. The actions, when received
        by Unity, are scaled to the size of the destination object and transformed
        into 3D space, so only these two values need to be provided by the RL client.
        Num     Action                          Min                     Max
        0       X value of object placement     -1.0                    1.0
        1       Y value of object placement     -1.0                    1.0
        
    Reward:
        Reward is 1 if an action results in a stack that is one object higher and
        stable. Reward is -1 if an action results in one object falling off the stack,
        -2 if it results in two objects falling off the stack, etc.
                
    Starting State:
        The starting observation is 1, because all objects are on the ground, therefore
        the "highest stack" is 1 block high.
                
    Episode Termination:
        All objects in the environment have been successfully stacked.
        Episode length is greater than the "Max Step" value specified in the
        "StackingAgent" class in the Unity Editor.
        Solved Requirements:
        Because the reward is contructed in terms of "did the theme object stay where
        it was placed?" vs. "did the theme object fall down?" a successful policy
        should learn to produce action values that are closer to [0.0,0.0] in this
        action space. Considered solved when the average return is greater than or
        equal to 0.75 over 100 trials, meaning a 3/4 probability of placing the theme
        object in a stable position on the first try, with no more than one failed
        try per episode.
    """
    
    def __init__(self,
        environment_filename=None):
        self._env = UnityEnvironment(environment_filename,0)
        self.seed(42)
        self.resetting = False
        self.reset()
        
        # get behavior name from Unity
        self.behavior_name = list(self._env.behavior_specs)[0]
        print(self.behavior_name)

        # get behavior spec
        self.behavior_spec = self._env.behavior_specs[self.behavior_name]
        print(self.behavior_spec)

        # define action and observation space
        # action: where on the surface of the target block do I put my object?
        # observation: how tall is the tallest thing in the world?
        self.action_space = spaces.Box(np.array([-1,-1]),
            np.array([1,1]))
        #self.observation_space = spaces.Discrete(4)
        self.observation_space = spaces.Box(np.array([0]),np.array([4]))
        
        self.last_action = np.array([-float('inf'),-float('inf')])

    def step(self, action):
        if self.resetting:
            return
        if not np.allclose(action,self.last_action):
            print("action:",action)
        #self._env.set_actions(self.behavior_name, ActionTuple(continuous=np.array([.55,.55]).reshape((1,-1))))
        self._env.set_actions(self.behavior_name, ActionTuple(continuous=action.reshape((1,-1))))
        print("action shape:", action.reshape((1, -1)).shape)
        self._env.step()
        step_info, terminal_info = self._env.get_steps(self.behavior_name)
        print("step_info",len(step_info))

        obs = step_info.obs

        if step_info.obs[0].shape[0] > 0:
            obs = step_info.obs[0][0]
        else:
            print("step_info.obs[0].shape = ",step_info.obs[0].shape, "setting obs to 0")
            obs = 0

        if not np.allclose(action,self.last_action):
            print("observation:",obs)

        if step_info.reward.shape[0] > 0:
            reward = step_info.reward[0]
            print("reward in the step_info", reward)
        else:
            print("step_info.reward.shape = ",step_info.reward.shape, "setting reward to 0")
            reward = 0
            
        if not np.allclose(action,self.last_action):
            print("reward:",reward)

        done = (len(terminal_info) != 0)

        if done:
            print("Terminated\n\tObservation: %s\tReward: %s\tInterrupted = %s" % (terminal_info.obs[0] if terminal_info.obs is not None else terminal_info.obs,terminal_info.reward,terminal_info.interrupted))
            obs = terminal_info.obs[0][0]
            reward = terminal_info.reward[0]
            self.last_action = np.array([-float('inf'),-float('inf')])
            print("last_action", self.last_action)

        else:
            self.last_action = action
        info = {}
        return obs, reward, done, info

    def reset(self):
        print("Resetting")
        self.resetting = True
        self.seed()
        obs = self._env.reset()
        if obs is None:
            obs = np.array([1+np.random.normal(0,0.1,1)[0]]) # add gaussian noise
            print("obs is None, setting obs to", obs)
        self.resetting = False
        return obs

    #def render(self, mode='rgb_array'):
        #return self.visual_obs

    def close(self):
        self._env.close()
        
    def seed(self, seed=None):
        """Sets a fixed seed for this env's random number generator(s).
        The valid range for seeds is [0, 99999). By default a random seed
        will be chosen.
        """
        if seed is None:
            self._seed = seed
            return

        seed = int(seed)
        if seed < 0 or seed >= 99999:
            print("Seed outside of valid range [0, 99999). A random seed within the valid range will be used on next reset.")
        print("New seed " + str(seed) + " will apply on next reset.")
        self._seed = seed
