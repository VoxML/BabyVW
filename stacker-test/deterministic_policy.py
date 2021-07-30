import numpy as np
from stable_baselines3 import TD3
from test_policy import TestPolicy
import os
import matplotlib.pyplot as plt
from stable_baselines3.common.noise import NormalActionNoise, OrnsteinUhlenbeckActionNoise
from stacker_env import StackerEnv
from stable_baselines3.common.monitor import Monitor
import pandas as pd
import argparse

def main():
    parser = argparse.ArgumentParser(description='Stacker parameters. (Example usage: "python ddpg.py -b stacker -l cube_stacking_model -t 500 --train" OR "python ddpg.py -b stacker -t 50 --test")')
    parser.add_argument('--log_dir', '-l', metavar='LOGDIR', default='.', help='log directory')
    parser.add_argument('--tb_name', '-b', metavar='TBNAME', default='.', help='TensorBoard path name')
    parser.add_argument('--total_timesteps', '-t', metavar='TOTALTIMESTEPS', default=500, help='total timesteps')
    parser.add_argument('--model_name', '-m', metavar='MODELNAME', default='ddpg_saved', help='name of model to save/load')
    parser.add_argument('--train', action='store_true', default=False, help='train mode')
    parser.add_argument('--test', action='store_true', default=False, help='test mode')

    args = parser.parse_args()

    log_dir = args.log_dir
    tb_name = args.tb_name
    total_timesteps = int(args.total_timesteps)
    model_name = args.model_name
    train = args.train
    test = args.test
    
    os.makedirs(log_dir, exist_ok=True)

    env = StackerEnv()
    env = Monitor(env, log_dir)

    print("observation_space:", env.observation_space.shape)
    print("action_space_high:", env.action_space.high)
    print("action_space_low:", env.action_space.low)

    n_actions = env.action_space.shape[-1]
    print("n_actions", n_actions)

    action_noise = NormalActionNoise(mean=np.zeros(n_actions), sigma=0.1 * np.ones(n_actions))

    if train:
        model = TD3(TestPolicy, env, target_policy_noise = 0.002, learning_rate=1e-4, action_noise=action_noise, verbose=1, tensorboard_log="./" + tb_name + "/")
        model.learn(total_timesteps=total_timesteps)

        print("Done learning")

        model.save(log_dir + "/" + model_name)

        print("Model saved")

    if test:
        model = TD3.load(log_dir + "/" + model_name)
        print(model)
        print(model.policy)
        print(model.get_parameters())

        obs = env.reset()

        i = 0
        while True:
            print("Testing %s" % i)
            print("obs:", obs)
            action, _states = model.predict(obs)
            print("action:", action)
            last_obs = obs
            obs, rewards, dones, info = env.step(action)
            print("obs:", obs)
            print(dones)
            if dones:
                env.reset()
                
            if obs != last_obs:
                i += 1
                
            if i == total_timesteps:
                break

if __name__ == "__main__":
    main()
