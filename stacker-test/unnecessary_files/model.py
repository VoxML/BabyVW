import numpy as np

import torch
import torch.nn as nn
import torch.nn.functional as F


class Actor(nn.Module):

    #Actor (Policy) model

    def __init__(self, state_size, action_size, h1_units=200, h2_units=100):

            #state_size : Dimension of each state
            #action_size : Dimension of each action

        super(Actor, self).__init__()
        self.linear1 = nn.Linear(state_size, h1_units)
        torch.nn.init.xavier_uniform_(self.linear1.weight)
        self.linear2 = nn.Linear(h1_units, h2_units)
        torch.nn.init.xavier_uniform_(self.linear2.weight)
        self.linear3 = nn.Linear(h2_units, action_size)
        torch.nn.init.xavier_uniform_(self.linear3.weight)


    def forward(self, state):

        #mapping states to actions

        x = F.relu(self.linear1(state))
        x = F.relu(self.linear2(x))
        x = torch.tanh(self.linear3(x))
        return x


class Critic(nn.Module):

    #Critic (Value) model

    def __init__(self, state_size, action_size, h1_units=200, h2_units=100):

        #state_size : Dimension of each state
        #action_size : Dimension of each action

        super(Critic, self).__init__()
        self.linear1 = nn.Linear(state_size, h1_units)
        torch.nn.init.xavier_uniform_(self.linear1.weight)
        self.linear2 = nn.Linear(h1_units+action_size, h2_units)
        torch.nn.init.xavier_uniform_(self.linear2.weight)
        self.linear3 = nn.Linear(h2_units, 1)
        torch.nn.init.xavier_uniform_(self.linear3.weight)


    def forward(self, state, action):

        #mapping (state, action) pairs to Q-values

        xs = F.relu(self.linear1(state))
        x_cat = torch.cat((xs, action), dim = 1)
        x = F.relu(self.linear2(x_cat))
        x = self.linear3(x)
        return x
