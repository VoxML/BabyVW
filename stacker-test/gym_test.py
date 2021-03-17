import gym
import numpy as np
from stacker_env import StackerEnv

import time

def valid_obs(obs):
    return obs is not None and obs[0].shape[0] > 0 and obs[0][0] != -1

def main():
    env = StackerEnv()
    num_episodes = 3
    
    for _ in range(num_episodes):
        obs = None
        done = False
        t = 0
        while not done:
            if valid_obs(obs) and t != 0:
                action = env.action_space.sample()
            else:
                action = np.array([-float('inf'),-float('inf')])
                
            print("Time %s\n\tObservation: %s\tAction: %s" % (t,obs if obs is not None else obs,action))
                
            obs, reward, done, info = env.step(action)
            
            obs = np.array([[obs]])
            if valid_obs(obs):
                print(reward)
                print(reward)
                print("Observation: %s, Reward: %s, Done = %s\n" %
                    (obs[0][0] if obs is not None else obs,np.array([reward])[0],done))
            t += 1
            
        obs = env.reset()

if __name__ == '__main__':
    main()
