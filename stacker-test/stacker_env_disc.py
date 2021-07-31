import numpy as np
import gym
from gym import spaces

from mlagents_envs.environment import UnityEnvironment
from mlagents_envs.base_env import ActionTuple


class StackerEnv(gym.Env):
    def __init__(self,
        environment_filename=None,
        visual_observation=False,
        vector_observation=False):
        self._env = UnityEnvironment(environment_filename, 0)
        self.seed(42)
        self.resetting = False
        self.num_timesteps = 0
        
        self.visual_obs = visual_observation
        self.vector_obs = vector_observation
        self.dict_obs = self.visual_obs and self.vector_obs
        
        self.raw_image_space = spaces.Box(
            0,
            255,
            dtype=np.uint8,
            shape=(84, 84, 3)
        )
        
        self.normalized_image_space = spaces.Box(
            0.0,
            1.0,
            dtype=np.float32,
            shape=(84, 84, 3)
        )
        
        self.vector_obs_space = spaces.Box(
            0.0,
            4.0,
            dtype=np.float32,
            shape=(1,)
        )
        
        if self.dict_obs:
            self.image_space = self.normalized_image_space
        elif self.visual_obs:
            self.image_space = self.raw_image_space
            
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
        #self.action_space = spaces.Box(np.array([-1, -1]),
        #                               np.array([1, 1]))

        self.action_space = spaces.MultiDiscrete([3,3])

        if self.dict_obs:
            self.observation_space = spaces.Dict(
                spaces={
                    "visual_obs" : self.image_space,
                    "vector_obs" : self.vector_obs_space
                })
        elif self.visual_obs:
            self.observation_space = self.image_space
        elif self.vector_obs:
            self.observation_space = self.vector_obs_space
            
        self.last_action = np.array([-float('inf'), -float('inf')])
        self.last_obs = {}

    def step(self, action):
        self.num_timesteps += 1
        if self.resetting:
            return

        if not np.allclose(action, self.last_action):
            print("action:", action)

        dx = action[0]
        print("dx:", dx)

        dy = action[1]
        print("dy:", dy)

        epsilon = 1e-4

        if dx == 0:
            self.action_continuous[0] -= epsilon

        if dx == 1:
            self.action_continuous[0] += 0

        if dx == 2:
            self.action_continuous[0] += epsilon

        if dy == 0:
            self.action_continuous[1] -= epsilon

        if dy == 1:
            self.action_continuous[1] += 0

        if dy == 2:
            self.action_continuous[1] += epsilon
        
        self._env.set_actions(self.behavior_name, ActionTuple(discrete=action.reshape((1,-1))))
        print("action shape:", action.reshape((1, -1)).shape)
        self._env.step()
        step_info, terminal_info = self._env.get_steps(self.behavior_name)
        print("step_info",len(step_info))

        obs = 1
        
        if self.dict_obs:
            obs = {}
                        
            if step_info.obs[0].shape[0] > 0:
                obs["visual_obs"] = step_info.obs[0]
            else:
                print("step_info.obs[0].shape = ", step_info.obs[0].shape, "setting visual_obs to black")
                obs["visual_obs"] = np.zeros(self.image_space.shape, dtype=self.image_space.dtype)
                
            if step_info.obs[1].shape[0] > 0:
                obs["vector_obs"] = step_info.obs[1]
            else:
                print("step_info.obs[1].shape = ", step_info.obs[1].shape, "setting vector_obs to 0")
                obs["vector_obs"] = 0
        else:
            print(step_info.obs)
            if step_info.obs[0].shape[0] > 0:
                obs = step_info.obs[0]
                
                if self.visual_obs:
                    obs = (obs*255).astype('float32')
            else:
                if self.visual_obs:
                    print("step_info.obs[0].shape = ", step_info.obs[0].shape[0], "setting visual_obs to black")
                    obs = np.zeros(self.image_space.shape, dtype=self.image_space.dtype)
                elif self.vector_obs:
                    print("step_info.obs[0].shape = ", step_info.obs[0].shape[0], "setting vector_obs to 0")
                    obs = 0

        if not np.allclose(action,self.last_action):
            print("last observation:",self.last_obs)
            print("observation:",obs)

        if step_info.reward.shape[0] > 0:
            reward = step_info.reward[0]
            print("reward from step_info", reward)
        else:
            print("step_info.reward.shape = ", step_info.reward.shape, "setting reward to 0")
            reward = 0
            
        if not np.allclose(action,self.last_action):
            print("reward:",reward)

        done = (len(terminal_info) != 0)

        if done:
            print("Terminated\n\tObservation: %s\tReward: %s\tInterrupted = %s" % (terminal_info.obs,terminal_info.reward,terminal_info.interrupted))
            
            if self.dict_obs:
                obs["visual_obs"] = terminal_info.obs[0]
                obs["vector_obs"] = terminal_info.obs[1]
            else:
                obs = terminal_info.obs[0]
            reward = terminal_info.reward[0]
            self.last_action = np.array([-float('inf'),-float('inf')])
            print("last_action", self.last_action)
        else:
            self.last_obs = obs
            self.last_action = action
        info = {}
        return obs, reward, done, info

    def reset(self):
        print("Resetting")
        self.resetting = True
        self.seed()
        obs = self._env.reset()
        self.action_continuous = np.zeros(2, dtype=np.float64)
        if obs is None:
            if self.dict_obs:
                obs = {}
                obs["visual_obs"] = np.zeros(self.image_space.shape, dtype=self.image_space.dtype)
                obs["vector_obs"] = np.array([1+np.random.normal(0,0.1,1)[0]]) # add gaussian noise
            elif self.visual_obs:
                obs = np.zeros(self.image_space.shape, dtype=self.image_space.dtype)
            elif self.vector_obs:
                obs = np.array([1+np.random.normal(0,0.1,1)[0]]) # add gaussian noise
            print("obs is None, setting obs to", obs)
        self.resetting = False
        return obs

    # def render(self, mode='rgb_array'):
    # return self.visual_obs

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
            print(
                "Seed outside of valid range [0, 99999). A random seed within the valid range will be used on next reset.")
        print("New seed " + str(seed) + " will apply on next reset.")
        self._seed = seed
