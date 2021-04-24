import gym
import torch
import numpy as np
from collections import deque
import matplotlib.pyplot as plt
from stacker_env import StackerEnv
from ddpg import Agent


env = StackerEnv()
env.seed(0)
agent = Agent(state_size=1, action_size=2, random_seed=2)


def main(num_episodes = 1000, max_step = 300, buff_thr = 100):
    s_deque = deque(maxlen=buff_thr)
    rewards = []
    for episode in range(1, num_episodes + 1):
        state = env.reset()
        print(f"state: {state}")
        agent.reset()
        reward_init = 0
        for step in range(max_step):
            action = agent.act(state)
            #print(f"action: {action}")
            next_state, reward, done, _ = env.step(action)
            agent.step(state, action, reward, next_state, done)
            #print(f"action after applying step method: {action}")
            state = next_state
            reward_init += reward
            if done:
                break
        #s_deque.append(reward_init) # for mean reward
        rewards.append(reward_init)

    return rewards

rewards = main()

fig = plt.figure()
plt.plot(np.arange(1, len(rewards)+1), rewards)
plt.xlabel('Episode')
plt.ylabel('Reward')
plt.show()
