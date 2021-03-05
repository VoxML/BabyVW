import argparse
import numpy as np
import os
os.environ["CUDA_VISIBLE_DEVICES"]="-1" #comment this line if you want to use cuda
import pickle
from tensorflow.keras.models import Sequential, Model
from tensorflow.keras.layers import Dense, Activation, Flatten, Input, Concatenate
from tensorflow.keras.optimizers import Adam

from rl.agents import DDPGAgent
from rl.memory import SequentialMemory
from rl.random import OrnsteinUhlenbeckProcess

from stacker_env import StackerEnv

def main():
    parser = argparse.ArgumentParser(description='Stacker')
    parser.add_argument('-m', '--mode', metavar='MODE', help='mode (train/test)')
    parser.add_argument('-f', '--filename', metavar='FILENAME', help='model filename')
    parser.add_argument('-n', '--n_train_step', metavar='TRAINING', help='number of training episodes')
    parser.add_argument('-N', '--n_test_ep', metavar='TESTING', help='number of testing episodes')
    args = parser.parse_args()
    
    mode = args.mode
    filename = args.filename
    n_train_step = 1 if args.n_train_step is None else int(args.n_train_step)
    n_test_ep = 1 if args.n_test_ep is None else int(args.n_test_ep)

    # Get the environment and extract the number of actions.
    env = StackerEnv()
    #env.seed(np.random.seed())
    env.seed(0)
    num_actions = 2

    print("shape:",(1,) + env.observation_space.shape)

    # Actor
    actor = Sequential()
    actor.add(Input(shape=(1,)))
    actor.add(Dense(400))
    actor.add(Activation('relu'))
    actor.add(Dense(300))
    actor.add(Activation('relu'))
    actor.add(Dense(num_actions))
    actor.add(Activation('softsign'))
    print(actor.summary())

    # Critic
    action_input = Input(shape=(num_actions,), name='action_input')
    observation_input = Input(shape=(1,), name='observation_input')
    print(observation_input.shape)
    flattened_observation = Flatten()(observation_input)
    x = Concatenate()([action_input, flattened_observation])
    x = Dense(400)(x)
    x = Activation('relu')(x)
    x = Dense(300)(x)
    x = Activation('relu')(x)
    x = Dense(1)(x)
    x = Activation('linear')(x)
    critic = Model(inputs=[action_input, observation_input], outputs=x)
    print(critic.summary())

    memory = SequentialMemory(limit=2000, window_length=1)
    random_process = OrnsteinUhlenbeckProcess(size=num_actions, theta=0.6, mu=0, sigma=0.3)
    agent = DDPGAgent(nb_actions=num_actions, actor=actor, critic=critic, critic_action_input=action_input,
                      memory=memory, nb_steps_warmup_critic=2000, nb_steps_warmup_actor=10000,
                      random_process=random_process, gamma=.99, target_model_update=1e-3)
    agent.compile(Adam(lr=0.01,  clipnorm=1.), metrics=['mae'])

    if mode == "train":
        hist = agent.fit(env, nb_steps=n_train_step, action_repetition=7, visualize=False, verbose=2, nb_max_episode_steps=1000)

        if not os.path.exists('_experiments'):
            os.makedirs('_experiments')

        # save the history of learning, it can further be used to plot reward evolution
        with open('_experiments/history_ddpg_'+filename+'.pickle', 'wb') as handle:
                 pickle.dump(hist.history, handle, protocol=pickle.HIGHEST_PROTOCOL)
        # save the final weights
        agent.save_weights('h5f_files/ddpg_{}_weights.h5f'.format(filename), overwrite=True)

        # evaluate our algorithm for a couple episodes
        agent.test(env, nb_episodes=n_test_ep, visualize=False, nb_max_episode_steps=1000)
    elif mode == "test":
        agent.load_weights('h5f_files/ddpg_{}_weights.h5f'.format(filename))
        agent.test(env, nb_episodes=n_test_ep, nb_max_start_steps=3, visualize=False, nb_max_episode_steps=1000)

if __name__ == '__main__':
    main()
