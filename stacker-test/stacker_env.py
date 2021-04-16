import numpy as np
import gym
from gym import spaces

from mlagents_envs.environment import UnityEnvironment
from mlagents_envs.base_env import ActionTuple

class StackerEnv(gym.Env):
    def __init__(self,
        environment_filename=None):
        self._env = UnityEnvironment(environment_filename,0)
        #self.seed()
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
        self.action_space = spaces.Box(np.array([-100,-100]),
            np.array([100,100]))
        self.observation_space = spaces.Discrete(3)
        
        self.last_action = np.array([-float('inf'),-float('inf')])

    def step(self, action):
        if not np.allclose(action,self.last_action):
            print("action:",action)
        #self._env.set_actions(self.behavior_name, ActionTuple(continuous=np.array([.55,.55]).reshape((1,-1))))
        self._env.set_actions(self.behavior_name, ActionTuple(continuous=action.reshape((1,-1))))
        self._env.step()
        step_info, terminal_info = self._env.get_steps(self.behavior_name)
        obs = step_info.obs
        if step_info.obs[0].shape[0] > 0:
            obs = step_info.obs[0][0][0]
        else:
            obs = 0
        if not np.allclose(action,self.last_action):
            print("observation:",obs)
        if step_info.reward.shape[0] > 0:
            reward = step_info.reward[0]
        else:
            reward = step_info.reward
        if not np.allclose(action,self.last_action):
            print("reward:",reward)
        done = (len(terminal_info) != 0)
        if done:
            print("Terminated\n\tObservation: %s\tReward: %s\tInterrupted = %s" % (terminal_info.obs[0] if terminal_info.obs is not None else terminal_info.obs,terminal_info.reward,terminal_info.interrupted))
            #obs = terminal_info.obs
            reward = terminal_info.reward[0]
        info = {}
        self.last_action = action
        return obs, reward, done, info

    def reset(self):
        print("Resetting")
        self.seed()
        obs = self._env.reset()
        if obs is None:
            obs = 0
        return obs

    def render(self, mode='rgb_array'):
        return self.visual_obs

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
