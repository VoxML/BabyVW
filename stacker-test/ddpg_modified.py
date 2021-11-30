import numpy as np
from stable_baselines3 import DDPG, TD3
import os
from stable_baselines3.common.noise import NormalActionNoise
from stacker_env_modified import StackerEnv
from stable_baselines3.common.monitor import Monitor
import argparse, textwrap



def main():
    parser = argparse.ArgumentParser(description=textwrap.dedent('''
        Stacker parameters.
        Example usage:
        \tTrain new model for 500 timesteps using vector observations only, and save the model in the "cube_stacking_model" directory: "python ddpg.py -b stacker -l cube_stacking_model -t 500 --train --vector_obs"
        \tTest ddpg1 for 50 timesteps: "python ddpg.py -b stacker -m ddpg1 -t 50 --test"
        \tLoad ddpg1, continue training for 100 timesteps, and save the result as "ddpg2": "python ddpg.py -b stacker -m ddpg1 -M ddpg2" -t 100 --train"
        '''), formatter_class=argparse.RawTextHelpFormatter)
    parser.add_argument('--log_dir', '-l', metavar='LOGDIR', default='.', help='log directory')
    parser.add_argument('--tb_name', '-b', metavar='TBNAME', default='.', help='TensorBoard path name')
    parser.add_argument('--total_timesteps', '-t', metavar='TOTALTIMESTEPS', default=500, help='total timesteps')
    parser.add_argument('--model_name', '-m', metavar='MODELNAME', default='ddpg_saved',
                        help='name of model to save/load')
    parser.add_argument('--new_model_name', '-M', metavar='NEWMODELNAME', default=None,
                        help='name of new model to save if fine-tuning starting with another model')
    parser.add_argument('--visual_obs', action='store_true', default=False,
                        help='use visual observations (leave blank to use both)')
    parser.add_argument('--vector_obs', action='store_true', default=False,
                        help='use vector observations (leave blank to use both)')
    parser.add_argument('--train', action='store_true', default=False, help='train mode')
    parser.add_argument('--test', action='store_true', default=False, help='test mode')

    args = parser.parse_args()

    log_dir = args.log_dir
    tb_name = args.tb_name
    total_timesteps = int(args.total_timesteps)
    model_name = args.model_name
    new_model_name = args.new_model_name
    visual_obs = args.visual_obs
    vector_obs = args.vector_obs
    train = args.train
    test = args.test

    if not visual_obs and not vector_obs:
        visual_obs = True
        vector_obs = True

    os.makedirs(log_dir, exist_ok=True)

    env = StackerEnv(visual_observation=visual_obs, vector_observation=vector_obs)

    # env = make_vec_env(lambda:env, n_envs=2)

    env = Monitor(env, log_dir)
    print("################observation_space shape##################")
    print("observation_space:", env.observation_space.shape)
    print("##########################################################")
    print("action_space_high:", env.action_space.high)
    print("action_space_low:", env.action_space.low)

    n_actions = env.action_space.shape[-1]
    print("n_actions", n_actions)

    action_noise = NormalActionNoise(mean=np.zeros(n_actions), sigma=0.1 * np.ones(n_actions))

    if train:
        if new_model_name is None:
            if visual_obs and vector_obs:
                model = DDPG("MultiInputPolicy", env, learning_rate=1e-4, action_noise=action_noise, verbose=1,
                             tensorboard_log="./" + tb_name + "/")
            elif visual_obs:
                model = DDPG("CnnPolicy", env, learning_rate=1e-4, action_noise=action_noise, verbose=1,
                             tensorboard_log="./" + tb_name + "/")
            elif vector_obs:
                model = TD3("MlpPolicy", env, learning_rate=1e-5, target_policy_noise=0.0002, action_noise=action_noise,
                            verbose=1,
                            tensorboard_log="./" + tb_name + "/")
        else:
            model = TD3.load(log_dir + "/" + model_name, env)

        model.learn(total_timesteps=total_timesteps)

        if new_model_name is None:
            model.save(log_dir + "/" + model_name)
            print("Model saved at", log_dir + "/" + model_name)
        else:
            model.save(log_dir + "/" + new_model_name)
            print("Model saved at", log_dir + "/" + new_model_name)

        print("###################################################################")
        print("#################Done learning###################")
        print("###################################################################")
        print("###################################################################")
        print("#################Evaluating agent###################")
        print("###################################################################")

    num_episodes = 30
    env = model.get_env()
    all_episode_rewards = []
    for i in range(num_episodes):
        episode_rewards = []
        done = False
        obs = env.reset()
        while not done:
            action, states = model.predict(obs, deterministic=True)
            obs, reward, done, info = env.step(action)
            episode_rewards.append(reward)

        all_episode_rewards.append(sum(episode_rewards))

    mean_episode_reward = np.mean(all_episode_rewards)
    print("Mean reward:", mean_episode_reward, "Num episodes:", num_episodes)