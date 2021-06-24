import gym
import torch
import numpy as np
from collections import deque
import matplotlib.pyplot as plt
from stacker_env import StackerEnv
from ddpg import Agent


env = StackerEnv()
env.seed(0)
agent = Agent(state_size = 1, action_size = 2, random_seed = 2)


def main(num_episodes = 30, max_step = 100, buff_thr = 50):
    s_deque = deque(maxlen=buff_thr)
    rewards = []
    ep = 1
    for episode in range(1, num_episodes + 1):
        state = env.reset()
        print(f"state: {state}")
        agent.reset()
        reward_init = 0
        for step in range(max_step):
            action = agent.get_action(state)
            #print(f"action: {action}")
            next_state, reward, done, _ = env.step(action)
            agent.step(state, action, reward, next_state, done)
            #print(f"action after applying step method: {action}")
            state = next_state
            reward_init += reward
            if done:
                break

        print(f"done episode: {ep}")
        ep += 1

        s_deque.append(reward_init) # for mean reward
        rewards.append(reward_init)

        torch.save(agent.actor.state_dict(), 'checkpointactor.pth')
        torch.save(agent.critic.state_dict(), 'checkpointcritic.pth')

    return rewards

rewards = main()

#fig = plt.figure()
#plt.plot(np.arange(1, len(rewards)+1), rewards)
#plt.xlabel('Episode')
#plt.ylabel('Reward')
#plt.show()

"""agent.actor.load_state_dict(torch.load('checkpoint_actor.pth'))
agent.critic.load_state_dict(torch.load('checkpoint_critic.pth'))

state = env.reset()
for t in range(200):
    action = agent.get_action(state, add_noise=False)
    env.render()
    state, reward, done, _ = env.step(action)
    if done:
        break

env.close()"""